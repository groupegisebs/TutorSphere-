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
        => Ok(await _accounts.GetSetupAsync(ct));

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
}
