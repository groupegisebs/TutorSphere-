using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.LessonCoverage;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/parent/lesson-coverage")]
[Authorize(Roles = UserRoles.ParentPortalAccess)]
public class ParentLessonCoverageController(ILessonCoverageService coverage) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageDto>>> Pending(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(await coverage.ListPendingForParentAsync(userId, ct));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<LessonCoverageDto>> Respond(
        Guid id, [FromBody] RespondLessonCoverageRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        try
        {
            return Ok(await coverage.RespondAsParentAsync(userId, id, request.Approve, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/students/lesson-coverage")]
[Authorize(Roles = UserRoles.Student)]
public class StudentLessonCoverageController(ILessonCoverageService coverage) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<LessonCoverageDto>>> Pending(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(await coverage.ListPendingForStudentAsync(userId, ct));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<LessonCoverageDto>> Respond(
        Guid id, [FromBody] RespondLessonCoverageRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        try
        {
            return Ok(await coverage.RespondAsStudentAsync(userId, id, request.Approve, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
