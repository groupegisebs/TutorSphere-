using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert")]
[Authorize(Roles = UserRoles.Expert)]
public class ExpertApprovalsController : ControllerBase
{
    private readonly IExpertApprovalService _approvals;
    private readonly IExpertMonitoringService _monitoring;
    private readonly IExpertDashboardService _dashboard;
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExpertApprovalsController(
        IExpertApprovalService approvals,
        IExpertMonitoringService monitoring,
        IExpertDashboardService dashboard,
        IAuthService authService,
        UserManager<ApplicationUser> userManager)
    {
        _approvals = approvals;
        _monitoring = monitoring;
        _dashboard = dashboard;
        _authService = authService;
        _userManager = userManager;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<ExpertDashboardSummaryDto>> DashboardSummary(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _dashboard.GetSummaryAsync(UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("approvals-queue")]
    public async Task<ActionResult<IReadOnlyList<ExpertApprovalQueueItemDto>>> ApprovalsQueue(
        [FromQuery] string? country,
        [FromQuery] string? city,
        [FromQuery] ExpertApprovalStatus? status,
        [FromQuery] int? minDocuments,
        [FromQuery] bool? incompleteOnly,
        [FromQuery] bool? urgentOnly,
        [FromQuery] string? assignedToUserId,
        [FromQuery] int? olderThanDays,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var filter = new ExpertApprovalQueueFilter(
            country, city, status, minDocuments, incompleteOnly, urgentOnly, assignedToUserId, olderThanDays);
        var list = await _approvals.ListQueueForExpertAsync(UserId, filter, ct);
        var enriched = new List<ExpertApprovalQueueItemDto>();
        foreach (var item in list)
        {
            string? email = null;
            string? name = null;
            string? assigneeName = null;
            var detail = await _approvals.GetReviewDetailAsync(item.TenantId, ct);
            if (detail?.OwnerUserId is { } oid)
            {
                var user = await _userManager.FindByIdAsync(oid);
                email = user?.Email;
                name = user?.FullName;
            }
            if (!string.IsNullOrWhiteSpace(item.ReviewAssignedToUserId))
            {
                var a = await _userManager.FindByIdAsync(item.ReviewAssignedToUserId);
                assigneeName = a?.FullName;
            }
            enriched.Add(item with
            {
                OwnerEmail = email,
                OwnerName = name,
                ReviewAssignedToName = assigneeName
            });
        }
        return Ok(enriched);
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<PendingTeacherDto>>> Queue(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var list = await _approvals.ListPendingForExpertAsync(UserId, ct);
        var enriched = new List<PendingTeacherDto>();
        foreach (var item in list)
        {
            string? email = null;
            string? name = null;
            var detail = await _approvals.GetReviewDetailAsync(item.TenantId, ct);
            if (detail?.OwnerUserId is { } oid)
            {
                var user = await _userManager.FindByIdAsync(oid);
                email = user?.Email;
                name = user?.FullName;
            }

            enriched.Add(item with { OwnerEmail = email, OwnerName = name });
        }

        return Ok(enriched);
    }

    [HttpGet("teachers/{tenantId:guid}")]
    public async Task<ActionResult<TeacherReviewDetailDto>> GetTeacher(Guid tenantId, CancellationToken ct)
    {
        var detail = await _approvals.GetReviewDetailAsync(tenantId, ct);
        if (detail is null) return NotFound(new { error = "Fiche introuvable." });

        if (!string.IsNullOrWhiteSpace(detail.OwnerUserId))
        {
            var user = await _userManager.FindByIdAsync(detail.OwnerUserId);
            detail = detail with
            {
                OwnerEmail = user?.Email,
                OwnerName = user?.FullName
            };
        }

        return Ok(detail);
    }

    [HttpPost("teachers/{tenantId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid tenantId, [FromBody] ExpertDecisionRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.ApproveAsync(tenantId, UserId, request?.Notes, ct);
            return Ok(new { message = "Enseignant approuvé." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid tenantId, [FromBody] ExpertDecisionRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.RejectAsync(tenantId, UserId, request?.Notes, ct);
            return Ok(new { message = "Enseignant rejeté." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(Guid tenantId, [FromBody] RequestChangesRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.RequestChangesAsync(tenantId, UserId, request?.Notes ?? "", ct);
            return Ok(new { message = "Modifications demandées." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid tenantId, [FromBody] AssignReviewRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.AssignReviewAsync(tenantId, UserId, request ?? new AssignReviewRequest(null), ct);
            return Ok(new { message = "Dossier attribué." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/start-review")]
    public async Task<IActionResult> StartReview(Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.StartReviewAsync(tenantId, UserId, ct);
            return Ok(new { message = "Revue démarrée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("invite-application")]
    public async Task<IActionResult> InviteApplication([FromBody] InviteTeacherApplicationRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null)
            return BadRequest(new { error = "Requête invalide." });

        try
        {
            await _approvals.InviteTeacherApplicationAsync(UserId, request, ct);
            return Ok(new { message = "Invitation envoyée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("register-teacher")]
    public async Task<ActionResult<RegisterTeacherByExpertResponse>> RegisterTeacher(
        [FromBody] RegisterTeacherByExpertRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null)
            return BadRequest(new { error = "Requête invalide." });

        try
        {
            var result = await _authService.RegisterTeacherByExpertAsync(UserId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-group")]
    public async Task<ActionResult<ExpertMyGroupDto>> MyGroup(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var group = await _approvals.GetMyGroupAsync(UserId, ct);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpGet("me-context")]
    public async Task<ActionResult<object>> MeContext(
        [FromServices] IExpertGroupManagerService managers,
        [FromServices] IExpertGroupService groups,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var isManager = managers.IsActiveManager(UserId);
        Guid? groupId = null;
        string? groupName = null;
        var my = await _approvals.GetMyGroupAsync(UserId, ct);
        if (my is not null)
        {
            groupId = my.Id;
            groupName = my.Name;
        }
        return Ok(new { isGroupManager = isManager, groupId, groupName });
    }

    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<TeacherApplicationInviteDto>>> Invitations(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var list = await _approvals.ListInvitesForExpertAsync(UserId, ct);
        return Ok(list);
    }

    [HttpGet("teachers")]
    public async Task<ActionResult<IReadOnlyList<MonitoredTeacherDto>>> MonitoredTeachers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var list = await _monitoring.ListMonitoredTeachersAsync(UserId, ct);

        var enriched = new List<MonitoredTeacherDto>(list.Count);
        foreach (var item in list)
        {
            var detail = await _approvals.GetReviewDetailAsync(item.TenantId, ct);
            string? email = null;
            string? name = null;
            if (!string.IsNullOrWhiteSpace(detail?.OwnerUserId))
            {
                var user = await _userManager.FindByIdAsync(detail!.OwnerUserId);
                email = user?.Email;
                name = user?.FullName;
            }
            enriched.Add(item with { OwnerEmail = email, OwnerName = name });
        }

        return Ok(enriched);
    }

    [HttpGet("teachers/{tenantId:guid}/materials")]
    public async Task<ActionResult<IReadOnlyList<TeacherMaterialItemDto>>> Materials(Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var list = await _monitoring.GetTeacherMaterialsAsync(tenantId, UserId, ct);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("teachers/{tenantId:guid}/remarks")]
    public async Task<ActionResult<IReadOnlyList<ExpertRemarkDto>>> Remarks(Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var list = await _monitoring.ListRemarksAsync(tenantId, UserId, ct);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/remarks")]
    public async Task<ActionResult<ExpertRemarkDto>> AddRemark(
        Guid tenantId, [FromBody] CreateExpertRemarkRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });

        try
        {
            var dto = await _monitoring.AddRemarkAsync(UserId, tenantId, request, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
