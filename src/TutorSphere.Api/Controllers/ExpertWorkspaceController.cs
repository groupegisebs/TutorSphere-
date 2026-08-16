using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager}")]
public class ExpertWorkspaceController : ControllerBase
{
    private readonly IExpertWorkspaceService _workspace;
    private readonly IExpertGovernanceAuditService _audit;

    public ExpertWorkspaceController(IExpertWorkspaceService workspace, IExpertGovernanceAuditService audit)
    {
        _workspace = workspace;
        _audit = audit;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("workspace")]
    public async Task<ActionResult<IReadOnlyList<ExpertWorkspaceItemDto>>> List(
        [FromQuery] ExpertWorkspaceItemType type, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await _workspace.ListAsync(UserId, type, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("workspace")]
    public async Task<ActionResult<ExpertWorkspaceItemDto>> Create(
        [FromBody] CreateExpertWorkspaceItemRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try { return Ok(await _workspace.CreateAsync(UserId, request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("workspace/{id:guid}/start")]
    public async Task<ActionResult<ExpertWorkspaceItemDto>> Start(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await _workspace.StartAsync(id, UserId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("workspace/{id:guid}/payload")]
    public async Task<ActionResult<ExpertWorkspaceItemDto>> UpdatePayload(
        Guid id, [FromBody] UpdateWorkspacePayloadRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await _workspace.UpdatePayloadAsync(id, UserId, request?.PayloadJson, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("classroom/{lessonId:guid}")]
    public async Task<ActionResult<LessonDto>> GetClassroom(Guid lessonId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await _workspace.GetDemonstrationClassroomAsync(lessonId, UserId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("workspace/{id:guid}/complete")]
    public async Task<ActionResult<ExpertWorkspaceItemDto>> Complete(
        Guid id, [FromBody] CompleteExpertWorkspaceItemRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _workspace.CompleteAsync(id, UserId, request ?? new CompleteExpertWorkspaceItemRequest(), ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyList<ExpertGovernanceEventDto>>> Activity(
        [FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _audit.ListForGroupAsync(UserId, take, notificationsOnly: false, ct));
    }

    /// <summary>Journal d'activité paginé, avec recherche et filtre par type.</summary>
    [HttpGet("activity/page")]
    public async Task<ActionResult<ExpertGovernanceEventPageDto>> ActivityPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? type = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _audit.ListPageForGroupAsync(
            UserId, page, pageSize, notificationsOnly: false, type, search, ct));
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<ExpertGovernanceEventDto>>> Notifications(
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _audit.ListForGroupAsync(UserId, take, notificationsOnly: true, ct));
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _audit.MarkReadAsync(id, UserId, ct);
            return Ok(new { message = "Lu." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        await _audit.MarkAllNotificationsReadAsync(UserId, ct);
        return Ok(new { message = "Tout marqué comme lu." });
    }
}
