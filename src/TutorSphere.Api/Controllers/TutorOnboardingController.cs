using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Onboarding;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/tutor-onboarding")]
[Authorize(Roles = UserRoles.Tutor)]
public class TutorOnboardingController(ITutorOnboardingService onboarding) : ControllerBase
{
    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Utilisateur non authentifié.");

    private string Culture =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("status")]
    public async Task<ActionResult<TutorOnboardingStatusDto>> Status(CancellationToken ct)
    {
        try
        {
            return Ok(await onboarding.GetStatusAsync(UserId, Culture, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("modules/complete")]
    public async Task<ActionResult<CompleteOnboardingModuleResult>> CompleteModule(
        [FromBody] CompleteOnboardingModuleRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await onboarding.CompleteModuleAsync(UserId, request, Culture, ct);
            if (!string.IsNullOrEmpty(result.Error))
                return BadRequest(result);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
