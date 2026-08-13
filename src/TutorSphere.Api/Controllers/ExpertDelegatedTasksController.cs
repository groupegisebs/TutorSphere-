using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api")]
public class ExpertDelegatedTasksController : ControllerBase
{
    private readonly IExpertDelegatedTaskService _tasks;
    private readonly UserManager<ApplicationUser> _users;

    public ExpertDelegatedTasksController(IExpertDelegatedTaskService tasks, UserManager<ApplicationUser> users)
    {
        _tasks = tasks;
        _users = users;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("group-admin/tasks")]
    [Authorize(Roles = UserRoles.GroupManager)]
    public async Task<ActionResult<IReadOnlyList<ExpertDelegatedTaskDto>>> ListManager(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await EnrichAsync(await _tasks.ListForManagerAsync(UserId, ct)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("group-admin/tasks")]
    [Authorize(Roles = UserRoles.GroupManager)]
    public async Task<ActionResult<ExpertDelegatedTaskDto>> Create(
        [FromBody] CreateExpertDelegatedTaskRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var created = await _tasks.CreateAsync(UserId, request, ct);
            return Ok((await EnrichAsync([created])).First());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("group-admin/tasks/{taskId:guid}/cancel")]
    [Authorize(Roles = UserRoles.GroupManager)]
    public async Task<IActionResult> Cancel(Guid taskId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _tasks.CancelAsync(taskId, UserId, ct);
            return Ok(new { message = "Tâche annulée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/my-tasks")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager}")]
    public async Task<ActionResult<IReadOnlyList<ExpertDelegatedTaskDto>>> ListMine(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await EnrichAsync(await _tasks.ListForAssigneeAsync(UserId, ct)));
    }

    [HttpPost("expert/my-tasks/{taskId:guid}/start")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager}")]
    public async Task<ActionResult<ExpertDelegatedTaskDto>> Start(Guid taskId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok((await EnrichAsync([await _tasks.StartAsync(taskId, UserId, ct)])).First());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("expert/my-tasks/{taskId:guid}/complete")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager}")]
    public async Task<ActionResult<ExpertDelegatedTaskDto>> Complete(
        Guid taskId, [FromBody] CompleteExpertDelegatedTaskRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok((await EnrichAsync([await _tasks.CompleteAsync(taskId, UserId, request ?? new CompleteExpertDelegatedTaskRequest(), ct)])).First());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IReadOnlyList<ExpertDelegatedTaskDto>> EnrichAsync(IReadOnlyList<ExpertDelegatedTaskDto> list)
    {
        var result = new List<ExpertDelegatedTaskDto>();
        foreach (var t in list)
        {
            var user = await _users.FindByIdAsync(t.AssigneeExpertUserId);
            result.Add(t with { AssigneeName = user?.FullName });
        }
        return result;
    }
}
