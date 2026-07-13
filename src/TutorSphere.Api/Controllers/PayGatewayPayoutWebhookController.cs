using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Services;
using TutorSphere.Infrastructure.PayGateway;

namespace TutorSphere.Api.Controllers;

/// <summary>
/// Callbacks PayGateway pour les décaissements tuteur (après revue / rapprochement admin).
/// </summary>
[ApiController]
[Route("api/webhooks/paygateway")]
public class PayGatewayPayoutWebhookController(
    ITutorEarningsService earnings,
    IOptions<PayGatewaySettings> options,
    ILogger<PayGatewayPayoutWebhookController> logger) : ControllerBase
{
    [HttpPost("payouts")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-PayGateway-Signature"].ToString();
        var secret = options.Value.PayoutWebhookSecret;

        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (string.IsNullOrWhiteSpace(signature) || !Verify(secret, json, signature))
                return Unauthorized();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "unknown" : "unknown";

        try
        {
            if (eventType == "disbursement.paid" && root.TryGetProperty("data", out var paidData))
            {
                var key = paidData.TryGetProperty("idempotencyKey", out var ik) ? ik.GetString()
                    : paidData.TryGetProperty("externalReference", out var er) ? er.GetString()
                    : null;
                var providerPayoutId = paidData.TryGetProperty("providerPayoutId", out var pp) ? pp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(key))
                    await earnings.CompleteFromGatewayAsync(key, providerPayoutId, cancellationToken);
            }
            else if (eventType == "disbursement.rejected" && root.TryGetProperty("data", out var rejectedData))
            {
                var key = rejectedData.TryGetProperty("idempotencyKey", out var ik) ? ik.GetString()
                    : rejectedData.TryGetProperty("externalReference", out var er) ? er.GetString()
                    : null;
                var reason = rejectedData.TryGetProperty("reason", out var r) ? r.GetString() : "rejected";
                if (!string.IsNullOrWhiteSpace(key))
                    await earnings.RejectFromGatewayAsync(key, reason, cancellationToken);
            }

            return Ok(new { status = "ok" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook PayGateway payouts failed ({Event})", eventType);
            return StatusCode(500);
        }
    }

    private static bool Verify(string secret, string payload, string signature)
    {
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }
}
