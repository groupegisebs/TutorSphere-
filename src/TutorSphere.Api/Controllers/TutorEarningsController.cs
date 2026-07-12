using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.TutorEarnings;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/tutor-earnings")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
public class TutorEarningsController : ControllerBase
{
    private readonly ITutorEarningsService _earnings;

    public TutorEarningsController(ITutorEarningsService earnings) => _earnings = earnings;

    [HttpGet]
    public async Task<ActionResult<TutorEarningsSummaryDto>> GetSummary(CancellationToken ct)
        => Ok(await _earnings.GetSummaryAsync(ct));

    [HttpGet("eligibility")]
    public async Task<ActionResult<PayoutEligibilityDto>> GetEligibility(CancellationToken ct)
        => Ok(await _earnings.GetEligibilityAsync(ct));

    [HttpGet("payouts")]
    public async Task<ActionResult<IReadOnlyList<TutorPayoutDto>>> ListPayouts(CancellationToken ct)
        => Ok(await _earnings.ListPayoutsAsync(ct));

    /// <summary>
    /// Encaisser les gains (cours terminés). ≥ 100 $ CAD immédiat ; &lt; 100 $ délai 30 j ; &lt; 10 $ refusé.
    /// </summary>
    [HttpPost("payouts")]
    public async Task<ActionResult<TutorPayoutDto>> RequestPayout(
        [FromBody] RequestTutorPayoutRequest? request,
        CancellationToken ct)
    {
        try
        {
            var payout = await _earnings.RequestPayoutAsync(
                request ?? new RequestTutorPayoutRequest(null, null),
                ct);
            return Ok(payout);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
