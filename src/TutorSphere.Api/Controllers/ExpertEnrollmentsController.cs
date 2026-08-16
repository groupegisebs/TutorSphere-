using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/enrollments")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertEnrollmentsController(
    IStudentSubscriptionService subscriptions,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<ExpertPendingEnrollmentDto>>> Pending(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await subscriptions.ListPendingForExpertGroupAsync(UserId, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<ExpertPendingEnrollmentDto>> Accept(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await subscriptions.AcceptForExpertGroupAsync(UserId, id, ResolveGroup(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ExpertPendingEnrollmentDto>> Reject(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await subscriptions.RejectForExpertGroupAsync(UserId, id, ResolveGroup(), ct));
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
