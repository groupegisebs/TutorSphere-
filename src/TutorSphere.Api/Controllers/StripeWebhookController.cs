using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(IStripeService stripeService, ILogger<StripeWebhookController> logger)
    {
        _stripeService = stripeService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);

        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature))
            return BadRequest(new { error = "Missing Stripe-Signature header." });

        try
        {
            await _stripeService.HandleWebhookAsync(json, signature!, ct);
            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook processing failed.");
            return StatusCode(500, new { error = "Webhook processing failed." });
        }
    }
}
