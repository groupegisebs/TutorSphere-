using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.PlatformBilling;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/platform-billing")]
[Authorize(Roles = UserRoles.Tutor)]
public class PlatformBillingController(IPlatformBillingService billing) : ControllerBase
{
    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Utilisateur non authentifié.");

    [HttpGet("status")]
    public async Task<ActionResult<PlatformLicenseStatusDto>> Status(CancellationToken ct)
    {
        try
        {
            return Ok(await billing.GetStatusForOwnerAsync(UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<PlatformLicenseCheckoutResponse>> Checkout(
        [FromBody] CreatePlatformLicenseCheckoutRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await billing.CreateCheckoutAsync(UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<PlatformLicensePaymentStatusDto>> Confirm(
        [FromQuery] Guid? paymentId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await billing.ConfirmAsync(UserId, paymentId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
