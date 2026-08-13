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
    private readonly ISubscriptionOfferingService _offerings;
    private readonly ITeacherSchoolAdminService _teacherSchools;

    public ExpertApprovalsController(
        IExpertApprovalService approvals,
        IExpertMonitoringService monitoring,
        IExpertDashboardService dashboard,
        IAuthService authService,
        UserManager<ApplicationUser> userManager,
        ISubscriptionOfferingService offerings,
        ITeacherSchoolAdminService teacherSchools)
    {
        _approvals = approvals;
        _monitoring = monitoring;
        _dashboard = dashboard;
        _authService = authService;
        _userManager = userManager;
        _offerings = offerings;
        _teacherSchools = teacherSchools;
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
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var isManager = User.IsInRole(UserRoles.GroupManager) || managers.IsActiveManager(UserId);
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

    [HttpGet("my-group/settings")]
    [Authorize(Roles = UserRoles.GroupManager)]
    public async Task<ActionResult<object>> GetMyGroupSettings(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var group = await _approvals.GetMyGroupAsync(UserId, ct);
        if (group is null) return NotFound();
        // Reload description from full entity via dashboard service path — use approvals GetMyGroup extended
        return Ok(await _approvals.GetMyGroupSettingsAsync(UserId, ct));
    }

    [HttpPut("my-group/settings")]
    [Authorize(Roles = UserRoles.GroupManager)]
    public async Task<ActionResult<object>> UpdateMyGroupSettings(
        [FromBody] UpdateManagerGroupSettingsRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _approvals.UpdateMyGroupSettingsAsync(UserId, request?.Description, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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

    [HttpGet("teachers/{tenantId:guid}/offerings")]
    public async Task<ActionResult<IReadOnlyList<TutorSphere.Application.DTOs.SubscriptionOfferings.SubscriptionOfferingDto>>> ListTeacherOfferings(
        Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _monitoring.EnsureCanMonitorTeacher(tenantId, UserId);
            return Ok(await _offerings.GetForTenantAsync(tenantId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/offerings")]
    public async Task<ActionResult<TutorSphere.Application.DTOs.SubscriptionOfferings.SubscriptionOfferingDto>> CreateTeacherOffering(
        Guid tenantId,
        [FromBody] TutorSphere.Application.DTOs.SubscriptionOfferings.CreateSubscriptionOfferingRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });

        try
        {
            _monitoring.EnsureCanMonitorTeacher(tenantId, UserId);
            var created = await _offerings.CreateForTenantAsync(tenantId, request, ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("teachers/{tenantId:guid}/school")]
    public async Task<ActionResult<TutorSphere.Application.DTOs.Admin.TeacherSchoolRecordDto>> GetTeacherSchool(
        Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            var dto = await _teacherSchools.GetByTenantIdAsync(tenantId, ct);
            if (dto is null) return NotFound(new { error = "École introuvable." });

            if (!string.IsNullOrWhiteSpace(dto.OwnerUserId))
            {
                var user = await _userManager.FindByIdAsync(dto.OwnerUserId);
                if (user is not null)
                {
                    dto = dto with
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Phone = user.PhoneNumber,
                        OwnerEmail = user.Email ?? dto.OwnerEmail
                    };
                }
            }
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("teachers/{tenantId:guid}/school")]
    public async Task<ActionResult<TutorSphere.Application.DTOs.Admin.TeacherSchoolRecordDto>> UpdateTeacherSchool(
        Guid tenantId,
        [FromBody] TutorSphere.Application.DTOs.Admin.UpdateTeacherSchoolRecordRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            var current = await _teacherSchools.GetByTenantIdAsync(tenantId, ct)
                ?? throw new InvalidOperationException("École introuvable.");

            if (!string.IsNullOrWhiteSpace(current.OwnerUserId))
            {
                var user = await _userManager.FindByIdAsync(current.OwnerUserId);
                if (user is not null)
                {
                    if (!string.IsNullOrWhiteSpace(request.FirstName))
                        user.FirstName = request.FirstName.Trim();
                    if (!string.IsNullOrWhiteSpace(request.LastName))
                        user.LastName = request.LastName.Trim();
                    if (request.Phone is not null)
                        user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
                    var ur = await _userManager.UpdateAsync(user);
                    if (!ur.Succeeded)
                        return BadRequest(new { error = string.Join("; ", ur.Errors.Select(e => e.Description)) });
                }
            }

            var dto = await _teacherSchools.UpdateTenantProfileAsync(tenantId, request, ct);

            if (request.Publish == true && !dto.IsPublicProfile)
            {
                await _teacherSchools.PublishPublicProfileAsync(tenantId, UserId, asPlatformAdmin: false, ct);
                dto = (await _teacherSchools.GetByTenantIdAsync(tenantId, ct))!;
            }

            if (!string.IsNullOrWhiteSpace(dto.OwnerUserId))
            {
                var user = await _userManager.FindByIdAsync(dto.OwnerUserId);
                if (user is not null)
                {
                    dto = dto with
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Phone = user.PhoneNumber,
                        OwnerEmail = user.Email ?? dto.OwnerEmail
                    };
                }
            }
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/publish-profile")]
    public async Task<ActionResult<TutorSphere.Application.DTOs.Admin.PublishTeacherPublicProfileResult>> PublishTeacherProfile(
        Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _teacherSchools.PublishPublicProfileAsync(tenantId, UserId, asPlatformAdmin: false, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/unpublish-profile")]
    public async Task<ActionResult<TutorSphere.Application.DTOs.Admin.PublishTeacherPublicProfileResult>> UnpublishTeacherProfile(
        Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _teacherSchools.UnpublishPublicProfileAsync(tenantId, UserId, asPlatformAdmin: false, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
