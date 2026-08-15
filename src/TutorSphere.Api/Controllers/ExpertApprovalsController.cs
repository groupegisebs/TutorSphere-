using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertApprovalsController : ControllerBase
{
    private readonly IExpertApprovalService _approvals;
    private readonly IExpertMonitoringService _monitoring;
    private readonly IExpertDashboardService _dashboard;
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionOfferingService _offerings;
    private readonly ITeacherSchoolAdminService _teacherSchools;
    private readonly IGroupAdminAccessService _groupAccess;
    private readonly IBrandingService _branding;
    private readonly IExpertDisciplineService _disciplines;
    private readonly IWebHostEnvironment _env;

    public ExpertApprovalsController(
        IExpertApprovalService approvals,
        IExpertMonitoringService monitoring,
        IExpertDashboardService dashboard,
        IAuthService authService,
        UserManager<ApplicationUser> userManager,
        ISubscriptionOfferingService offerings,
        ITeacherSchoolAdminService teacherSchools,
        IGroupAdminAccessService groupAccess,
        IBrandingService branding,
        IExpertDisciplineService disciplines,
        IWebHostEnvironment env)
    {
        _approvals = approvals;
        _monitoring = monitoring;
        _dashboard = dashboard;
        _authService = authService;
        _userManager = userManager;
        _offerings = offerings;
        _teacherSchools = teacherSchools;
        _groupAccess = groupAccess;
        _branding = branding;
        _disciplines = disciplines;
        _env = env;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<ExpertDashboardSummaryDto>> DashboardSummary(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            Guid? overrideGroup = null;
            if (_groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
                overrideGroup = gid;
            return Ok(await _dashboard.GetSummaryAsync(UserId, ct, overrideGroup));
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
        Guid? overrideGroup = _groupAccess.IsPlatformAdmin(User) ? ActAsGroupId : null;
        var list = await _approvals.ListQueueForExpertAsync(UserId, filter, ct, overrideGroup);
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

    [HttpGet("teacher-decisions")]
    public async Task<ActionResult<IReadOnlyList<TeacherDecisionItemDto>>> TeacherDecisions(
        [FromQuery] DateTime? sinceUtc, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var since = sinceUtc?.ToUniversalTime()
                    ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        Guid? overrideGroup = _groupAccess.IsPlatformAdmin(User) ? ActAsGroupId : null;
        var list = await _approvals.ListRecentDecisionsAsync(UserId, since, ct, overrideGroup);
        return Ok(list);
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
        if (UserId is null) return Unauthorized();
        try
        {
            var asPlatform = _groupAccess.IsPlatformAdmin(User);
            await _approvals.EnsureCanViewTeacherAsync(
                tenantId, UserId, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);

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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid tenantId, [FromBody] ExpertDecisionRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _approvals.ApproveAsync(
                tenantId, UserId, request?.Notes, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
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
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _approvals.RejectAsync(
                tenantId, UserId, request?.Notes, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
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
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _approvals.RequestChangesAsync(
                tenantId, UserId, request?.Notes ?? "", ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
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
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _approvals.AssignReviewAsync(
                tenantId, UserId, request ?? new AssignReviewRequest(null), ct,
                asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
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
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _approvals.StartReviewAsync(
                tenantId, UserId, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
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
            var actAs = GroupAdminActAs.ReadGroupId(Request);
            var result = await _authService.RegisterTeacherByExpertAsync(UserId, request, ct, actAs);
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
        var group = await _groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct)
            ?? await _approvals.GetMyGroupAsync(UserId, ct);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpGet("me-context")]
    public async Task<ActionResult<object>> MeContext(
        [FromServices] IExpertGroupManagerService managers,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();

        var acting = await _groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct);
        if (acting is not null)
        {
            return Ok(new
            {
                isGroupManager = true,
                groupId = acting.Id,
                groupName = acting.Name,
                userId = UserId,
                isPlatformActAs = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue
            });
        }

        var isManager = managers.IsActiveManager(UserId);
        Guid? groupId = null;
        string? groupName = null;
        if (isManager)
        {
            var my = await _approvals.GetMyGroupAsync(UserId, ct);
            if (my is not null)
            {
                groupId = my.Id;
                groupName = my.Name;
            }
        }
        return Ok(new { isGroupManager = isManager, groupId, groupName, userId = UserId, isPlatformActAs = false });
    }

    [HttpGet("my-group/settings")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<object>> GetMyGroupSettings(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            if (_groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
            {
                var g = await _groupAccess.ResolveManagedGroupAsync(User, gid, ct)
                    ?? throw new InvalidOperationException("Groupe introuvable.");
                return Ok(g);
            }
            return Ok(await _approvals.GetMyGroupSettingsAsync(UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("my-group/settings")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<object>> UpdateMyGroupSettings(
        [FromBody] UpdateManagerGroupSettingsRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            if (_groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
            {
                // Platform act-as: update via approvals using group resolution
                var group = await _groupAccess.RequireManagedGroupIdAsync(User, gid, ct);
                return Ok(await _approvals.UpdateGroupSettingsAsAdminAsync(
                    group, request?.Description, request?.TeacherApprovalTrack, ct));
            }
            return Ok(await _approvals.UpdateMyGroupSettingsAsync(
                UserId, request?.Description, request?.TeacherApprovalTrack, ct));
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
        var list = await _monitoring.ListMonitoredTeachersAsync(
            UserId, ct, _groupAccess.IsPlatformAdmin(User) ? ActAsGroupId : null);

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

    [HttpGet("teacher-directory")]
    public async Task<ActionResult<IReadOnlyList<TeacherDirectoryItemDto>>> TeacherDirectory(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var list = await _monitoring.ListTeacherDirectoryAsync(
            UserId, ct, _groupAccess.IsPlatformAdmin(User) ? ActAsGroupId : null);
        return Ok(list);
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
            if (dto is null) return NotFound(new { error = "Profil introuvable." });

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
                ?? throw new InvalidOperationException("Profil introuvable.");

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

    [HttpGet("teachers/{tenantId:guid}/branding")]
    public async Task<ActionResult<TenantBrandingDto>> GetTeacherBranding(Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            var branding = await _branding.GetBrandingAsync(tenantId, ct);
            return Ok(branding ?? new TenantBrandingDto(
                Guid.Empty, tenantId, null, null, "#2563eb", "#1e40af", null, null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("teachers/{tenantId:guid}/branding")]
    public async Task<ActionResult<TenantBrandingDto>> UpdateTeacherBranding(
        Guid tenantId,
        [FromBody] UpdateTenantBrandingRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            return Ok(await _branding.UpdateBrandingAsync(tenantId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    [HttpPost("teachers/{tenantId:guid}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadTeacherPhoto(
        Guid tenantId, IFormFile file, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Photo trop volumineuse (max. 5 Mo)." });

        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
        {
            extension = file.ContentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ""
            };
        }
        if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
            return BadRequest(new { error = "Format non supporté. Utilisez PNG, JPG, GIF ou WebP." });

        var uploadsRoot = UploadsPaths.GetRoot(_env);
        var safeFileName = $"teacher-{tenantId:N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, safeFileName);
        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        var url = $"/uploads/{safeFileName}";
        try
        {
            var updated = await _branding.UpdateBrandingAsync(
                tenantId, new UpdateTenantBrandingRequest(LogoUrl: url), ct);
            return Ok(new { logoUrl = updated.LogoUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("teachers/{tenantId:guid}/disciplines")]
    public async Task<ActionResult<IReadOnlyList<TeacherDisciplineStatusDto>>> ListTeacherDisciplines(
        Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            return Ok(await _disciplines.ListAssignmentsForTeacherAsync(tenantId, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/disciplines/{disciplineId:guid}")]
    public async Task<IActionResult> AssignTeacherDiscipline(
        Guid tenantId, Guid disciplineId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            await _disciplines.AssignTeacherAsync(disciplineId, UserId, tenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("teachers/{tenantId:guid}/disciplines/{disciplineId:guid}")]
    public async Task<IActionResult> UnassignTeacherDiscipline(
        Guid tenantId, Guid disciplineId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            _teacherSchools.EnsureExpertCanManageTeacher(tenantId, UserId);
            await _disciplines.UnassignTeacherAsync(disciplineId, UserId, tenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
