using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/tutor-payout-accounts")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
public class TutorPayoutAccountsController : ControllerBase
{
    private readonly ITutorPayoutAccountService _accounts;

    public TutorPayoutAccountsController(ITutorPayoutAccountService accounts) => _accounts = accounts;

    [HttpGet("setup")]
    public async Task<ActionResult<TutorPayoutSetupDto>> GetSetup(CancellationToken ct)
    {
        try
        {
            return Ok(await _accounts.GetSetupAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("profile")]
    public async Task<ActionResult<TutorPayoutSetupDto>> UpdateProfile(
        [FromBody] UpdateTutorPayoutProfileRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _accounts.UpdateProfileAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TutorPayoutAccountDto>> Create(
        [FromBody] UpsertTutorPayoutAccountRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _accounts.UpsertAccountAsync(request, id: null, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TutorPayoutAccountDto>> Update(
        Guid id,
        [FromBody] UpsertTutorPayoutAccountRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _accounts.UpsertAccountAsync(request, id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
    {
        try
        {
            await _accounts.SetPrimaryAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try
        {
            await _accounts.DeactivateAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("stripe/onboard")]
    public async Task<IActionResult> StartStripeOnboard(
        [FromBody] OnboardUrlsRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _accounts.StartStripeConnectAsync(request.ReturnUrl, request.RefreshUrl, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("stripe/sync")]
    public async Task<IActionResult> SyncStripe(CancellationToken ct)
    {
        await _accounts.SyncStripeConnectAsync(ct);
        return Ok(await _accounts.GetSetupAsync(ct));
    }

    [HttpPost("paypal/oauth/start")]
    public async Task<IActionResult> StartPayPalOAuth(
        [FromBody] PayPalOAuthStartRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var result = await _accounts.StartPayPalOAuthAsync(request.ReturnUrl, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("paypal/oauth/complete")]
    public async Task<IActionResult> CompletePayPalOAuth(
        [FromBody] PayPalOAuthCompleteRequestDto request,
        CancellationToken ct)
    {
        try
        {
            await _accounts.CompletePayPalOAuthAsync(request.MaskedEmail, ct);
            return Ok(await _accounts.GetSetupAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public record OnboardUrlsRequest(string ReturnUrl, string RefreshUrl);
    public record PayPalOAuthStartRequestDto(string ReturnUrl);
    public record PayPalOAuthCompleteRequestDto(string? MaskedEmail);
}
