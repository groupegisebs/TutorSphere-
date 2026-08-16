using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.LessonCoverage;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/lesson-coverage")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertLessonCoverageController(
    ILessonCoverageService coverage,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet("unavailable")]
    public async Task<ActionResult<IReadOnlyList<UnavailableTeacherDto>>> Unavailable(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ListUnavailableTeachersAsync(UserId, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Enseignants approuvés du groupe, pour déclarer une absence à leur place.</summary>
    [HttpGet("teachers")]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageTeacherOptionDto>>> Teachers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ListGroupTeachersAsync(UserId, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("unavailable")]
    public async Task<ActionResult<UnavailableTeacherDto>> DeclareAbsence(
        [FromBody] DeclareTeacherAbsenceRequest request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.DeclareAbsenceAsync(UserId, request, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("unavailable/{id:guid}")]
    public async Task<IActionResult> DeleteAbsence(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await coverage.DeleteAbsenceAsync(UserId, id, ResolveGroup(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("substitutes")]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageTeacherOptionDto>>> Substitutes(
        [FromQuery] Guid originalTenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ListSubstituteOptionsAsync(UserId, originalTenantId, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("upcoming")]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageDto>>> Upcoming(
        [FromQuery] Guid originalTenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ListUpcomingLessonsAsync(
                UserId, originalTenantId, ResolveGroup(), from, to, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ListGroupAssignmentsAsync(UserId, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageDto>>> Propose(
        [FromBody] CreateLessonCoverageRequest request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await coverage.ProposeAsync(UserId, request, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await coverage.CancelAsync(UserId, id, ResolveGroup(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? ResolveGroup()
    {
        if (groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
            return gid;
        return ActAsGroupId;
    }
}
