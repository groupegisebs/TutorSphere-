using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

/// <summary>Remarques laissées par les experts, consultées côté enseignant (propriétaire de l'établissement).</summary>
[ApiController]
[Route("api/tutor/expert-remarks")]
[Authorize(Roles = UserRoles.Tutor)]
public class TeacherRemarksController(IExpertMonitoringService monitoring) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpertRemarkDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var list = await monitoring.ListRemarksForOwnerAsync(UserId, ct);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await monitoring.MarkRemarkReadAsync(id, UserId, ct);
            return Ok(new { message = "Remarque marquée comme lue." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
