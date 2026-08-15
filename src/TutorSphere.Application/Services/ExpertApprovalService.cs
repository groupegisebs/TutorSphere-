using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertApprovalService
{
    Task<IReadOnlyList<PendingTeacherDto>> ListPendingForExpertAsync(string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTeacherDto>> ListAllPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExpertApprovalQueueItemDto>> ListQueueForExpertAsync(
        string expertUserId,
        ExpertApprovalQueueFilter? filter = null,
        CancellationToken ct = default,
        Guid? overrideGroupId = null);
    Task<IReadOnlyList<TeacherDecisionItemDto>> ListRecentDecisionsAsync(
        string expertUserId,
        DateTime sinceUtc,
        CancellationToken ct = default,
        Guid? overrideGroupId = null);
    Task<TeacherReviewDetailDto?> GetReviewDetailAsync(Guid tenantId, CancellationToken ct = default);
    Task EnsureCanViewTeacherAsync(
        Guid tenantId,
        string callerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task ApproveAsync(
        Guid tenantId,
        string expertUserId,
        string? notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task RejectAsync(
        Guid tenantId,
        string expertUserId,
        string? notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task RequestChangesAsync(
        Guid tenantId,
        string expertUserId,
        string notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task AssignReviewAsync(
        Guid tenantId,
        string expertUserId,
        AssignReviewRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task StartReviewAsync(
        Guid tenantId,
        string expertUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task InviteTeacherApplicationAsync(
        string expertUserId,
        InviteTeacherApplicationRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task<IReadOnlyList<TeacherApplicationInviteDto>> ListInvitesForExpertAsync(string expertUserId, CancellationToken ct = default);
    Task MarkInviteAcceptedAsync(string email, Guid tenantId, string? inviteToken = null, CancellationToken ct = default);
    Task SyncInviteStatusForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TeacherApprovalStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetExpertGroupIdsAsync(string expertUserId, CancellationToken ct = default);
    Task<ExpertMyGroupDto?> GetMyGroupAsync(string expertUserId, CancellationToken ct = default);
    Task<ExpertMyGroupDto> GetMyGroupSettingsAsync(string managerUserId, CancellationToken ct = default);
    Task<ExpertMyGroupDto> UpdateMyGroupSettingsAsync(
        string managerUserId,
        string? description,
        int? teacherApprovalTrack = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        CancellationToken ct = default);
    Task<ExpertMyGroupDto> UpdateGroupSettingsAsAdminAsync(
        Guid groupId,
        string? description,
        int? teacherApprovalTrack = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        CancellationToken ct = default);
}

public class ExpertApprovalService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups,
    IExpertGroupManagerService managers,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls,
    IExpertGovernanceAuditService audit,
    ILogger<ExpertApprovalService> logger) : IExpertApprovalService
{
    public Task<IReadOnlyList<Guid>> GetExpertGroupIdsAsync(string expertUserId, CancellationToken ct = default)
    {
        IReadOnlyList<Guid> ids = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<ExpertMyGroupDto?> GetMyGroupAsync(string expertUserId, CancellationToken ct = default)
    {
        var groupId = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
        if (groupId == Guid.Empty)
            return Task.FromResult<ExpertMyGroupDto?>(null);

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId && g.IsActive);
        if (group is null)
            return Task.FromResult<ExpertMyGroupDto?>(null);

        return Task.FromResult<ExpertMyGroupDto?>(MapGroup(group));
    }

    public Task<ExpertMyGroupDto> GetMyGroupSettingsAsync(string managerUserId, CancellationToken ct = default)
    {
        var group = RequireManagedGroup(managerUserId);
        return Task.FromResult(MapGroup(group));
    }

    public async Task<ExpertMyGroupDto> UpdateMyGroupSettingsAsync(
        string managerUserId,
        string? description,
        int? teacherApprovalTrack = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        CancellationToken ct = default)
    {
        var group = RequireManagedGroup(managerUserId);
        return await UpdateGroupSettingsCoreAsync(group, description, teacherApprovalTrack, primaryColor, secondaryColor, ct);
    }

    public async Task<ExpertMyGroupDto> UpdateGroupSettingsAsAdminAsync(
        Guid groupId,
        string? description,
        int? teacherApprovalTrack = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        CancellationToken ct = default)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId && g.IsActive)
            ?? throw new InvalidOperationException("Groupe introuvable ou inactif.");
        return await UpdateGroupSettingsCoreAsync(group, description, teacherApprovalTrack, primaryColor, secondaryColor, ct);
    }

    private async Task<ExpertMyGroupDto> UpdateGroupSettingsCoreAsync(
        ExpertGroup group,
        string? description,
        int? teacherApprovalTrack,
        string? primaryColor,
        string? secondaryColor,
        CancellationToken ct)
    {
        group.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (teacherApprovalTrack.HasValue)
        {
            if (!Enum.IsDefined(typeof(TeacherApprovalTrack), teacherApprovalTrack.Value))
                throw new InvalidOperationException("Processus d'approbation inconnu.");
            group.TeacherApprovalTrack = (TeacherApprovalTrack)teacherApprovalTrack.Value;
        }
        if (primaryColor is not null)
            group.PrimaryColor = ColorHex.NormalizeOrNull(primaryColor);
        if (secondaryColor is not null)
            group.SecondaryColor = ColorHex.NormalizeOrNull(secondaryColor);
        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapGroup(group);
    }

    private static ExpertMyGroupDto MapGroup(ExpertGroup group) =>
        new(group.Id, group.Name, group.CountryCode, group.Description, group.IsInternational,
            (int)group.TeacherApprovalTrack, group.LogoUrl, group.BannerUrl, group.PrimaryColor, group.SecondaryColor);

    private ExpertGroup RequireManagedGroup(string managerUserId)
    {
        var membership = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.UserId == managerUserId
            && m.Status == ExpertMembershipStatus.Active
            && m.MemberRole == ExpertGroupMemberRole.Manager)
            ?? throw new InvalidOperationException("Accès réservé au Responsable actif du groupe.");

        return db.ExpertGroups.FirstOrDefault(g => g.Id == membership.ExpertGroupId && g.IsActive)
            ?? throw new InvalidOperationException("Groupe introuvable ou inactif.");
    }

    private void EnsureApprovalTrackSatisfied(ExpertGroup group, Guid tenantId)
    {
        var track = group.TeacherApprovalTrack;
        if (track.RequiresInterview())
        {
            var interviewDone = db.ExpertWorkspaceItems.Any(i =>
                i.ExpertGroupId == group.Id
                && i.RelatedTeacherTenantId == tenantId
                && i.ItemType == ExpertWorkspaceItemType.Interview
                && i.Status == ExpertWorkspaceItemStatus.Done);
            if (!interviewDone)
                throw new InvalidOperationException(
                    "Le processus du groupe exige un entretien clôturé avant l'approbation définitive.");
        }

        if (!track.RequiresDemonstration())
            return;

        var demos = db.ExpertWorkspaceItems
            .Where(i =>
                i.ExpertGroupId == group.Id
                && i.RelatedTeacherTenantId == tenantId
                && i.ItemType == ExpertWorkspaceItemType.Demonstration
                && i.Status == ExpertWorkspaceItemStatus.Done)
            .Select(i => i.PayloadJson)
            .ToList();

        var approvedDemo = demos.Any(json =>
            DemonstrationPayloadJson.RecommendationOf(json) == DemonstrationRecommendation.Approve);
        if (!approvedDemo)
            throw new InvalidOperationException(
                "Le processus du groupe exige une démonstration pédagogique avec recommandation « Approuver » avant l'approbation définitive.");
    }

    public Task<IReadOnlyList<PendingTeacherDto>> ListPendingForExpertAsync(string expertUserId, CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToList();

        if (groupIds.Count == 0)
            return Task.FromResult<IReadOnlyList<PendingTeacherDto>>([]);

        var activeGroupIds = db.ExpertGroups
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .Select(g => g.Id)
            .ToHashSet();
        if (activeGroupIds.Count == 0)
            return Task.FromResult<IReadOnlyList<PendingTeacherDto>>([]);

        var pending = db.Tenants
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Pending
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.Assigned
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.UnderReview
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.ChangesRequested)
            .OrderByDescending(t => t.ReviewPriority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var docCounts = db.TeacherDocumentsForAnyTenant
            .Where(d => pending.Select(t => t.Id).Contains(d.TenantId))
            .GroupBy(d => d.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        var result = new List<PendingTeacherDto>();
        foreach (var t in pending)
        {
            var suggested = expertGroups.ResolveReviewerGroup(t.Country);
            if (suggested is null || !activeGroupIds.Contains(suggested.Id))
                continue;

            result.Add(MapPending(t, suggested, docCounts.GetValueOrDefault(t.Id)));
        }

        return Task.FromResult<IReadOnlyList<PendingTeacherDto>>(result);
    }

    public Task<IReadOnlyList<PendingTeacherDto>> ListAllPendingAsync(CancellationToken ct = default)
    {
        var pending = db.Tenants
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Pending
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.Assigned
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.UnderReview
                        || t.ExpertApprovalStatus == ExpertApprovalStatus.ChangesRequested)
            .OrderByDescending(t => t.ReviewPriority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var docCounts = db.TeacherDocumentsForAnyTenant
            .Where(d => pending.Select(t => t.Id).Contains(d.TenantId))
            .GroupBy(d => d.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        IReadOnlyList<PendingTeacherDto> result = pending
            .Select(t =>
            {
                var suggested = expertGroups.ResolveReviewerGroup(t.Country);
                return MapPending(t, suggested, docCounts.GetValueOrDefault(t.Id));
            })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<TeacherReviewDetailDto?> GetReviewDetailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var t = db.Tenants.FirstOrDefault(x => x.Id == tenantId);
        if (t is null) return Task.FromResult<TeacherReviewDetailDto?>(null);

        var branding = db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenantId);
        var docs = db.TeacherDocumentsForAnyTenant
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        var suggested = expertGroups.ResolveReviewerGroup(t.Country);
        ExpertGroup? approvedGroup = null;
        if (t.ApprovedByExpertGroupId is Guid gid)
            approvedGroup = db.ExpertGroups.FirstOrDefault(g => g.Id == gid);

        var dto = new TeacherReviewDetailDto(
            t.Id,
            t.Name,
            t.Slug,
            t.Description,
            t.Country,
            t.City,
            t.Language,
            t.ExpertApprovalStatus,
            t.ExpertApprovalNotes,
            t.ExpertApprovedAt,
            t.ApprovedByExpertGroupId,
            approvedGroup?.Name,
            approvedGroup?.LogoUrl,
            t.OwnerUserId,
            null,
            null,
            branding?.Presentation,
            branding?.Portfolio,
            branding?.LogoUrl,
            docs.Select(MapDoc).ToList(),
            suggested?.Id,
            suggested?.Name);

        return Task.FromResult<TeacherReviewDetailDto?>(dto);
    }

    public Task EnsureCanViewTeacherAsync(
        Guid tenantId,
        string callerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Fiche introuvable.");

        var suggested = expertGroups.ResolveReviewerGroup(tenant.Country)
            ?? throw new InvalidOperationException("Aucun groupe d'experts pour cette fiche.");

        if (asPlatformAdmin && actAsGroupId is Guid actGid)
        {
            if (actGid != suggested.Id)
                throw new InvalidOperationException("Cette fiche n'appartient pas au groupe administré.");
            return Task.CompletedTask;
        }

        if (asPlatformAdmin)
            return Task.CompletedTask;

        EnsureExpertInGroup(callerUserId, suggested.Id);
        return Task.CompletedTask;
    }

    public async Task ApproveAsync(
        Guid tenantId,
        string expertUserId,
        string? notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var (tenant, group) = await RequireReviewableContextAsync(
            tenantId, expertUserId, ct, asPlatformAdmin, actAsGroupId);
        EnsureApprovalTrackSatisfied(group, tenant.Id);

        tenant.ExpertApprovalStatus = ExpertApprovalStatus.Approved;
        tenant.ApprovedByExpertGroupId = group.Id;
        tenant.ApprovedByUserId = expertUserId;
        tenant.ExpertApprovedAt = DateTime.UtcNow;
        tenant.ExpertApprovalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        // Visible publiquement seulement si licence + formation déjà OK.
        if (tenant.HasValidLicense())
            tenant.IsPublicProfile = true;

        await db.SaveChangesAsync(ct);
        await SyncInviteStatusForTenantAsync(tenant.Id, ct);
        await NotifyTeacherDecisionAsync(tenant, group.Name, approved: true, ct);
    }

    public async Task RejectAsync(
        Guid tenantId,
        string expertUserId,
        string? notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var (tenant, group) = await RequireReviewableContextAsync(
            tenantId, expertUserId, ct, asPlatformAdmin, actAsGroupId);

        if (string.IsNullOrWhiteSpace(notes))
            throw new InvalidOperationException("Un commentaire / motif est requis pour rejeter une demande.");

        tenant.ExpertApprovalStatus = ExpertApprovalStatus.Rejected;
        tenant.ApprovedByExpertGroupId = group.Id;
        tenant.ApprovedByUserId = expertUserId;
        tenant.ExpertApprovedAt = DateTime.UtcNow;
        tenant.ExpertApprovalNotes = notes.Trim();
        tenant.ExpertReviewNotifiedAt = null; // permet une nouvelle alerte si repasse en Pending
        tenant.IsPublicProfile = false;
        tenant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await SyncInviteStatusForTenantAsync(tenant.Id, ct);
        await NotifyTeacherDecisionAsync(tenant, group.Name, approved: false, ct);
    }

    public async Task RequestChangesAsync(
        Guid tenantId,
        string expertUserId,
        string notes,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var (tenant, _) = await RequireReviewableContextAsync(
            tenantId, expertUserId, ct, asPlatformAdmin, actAsGroupId);
        if (string.IsNullOrWhiteSpace(notes))
            throw new InvalidOperationException("Précisez les modifications demandées.");

        tenant.ExpertApprovalStatus = ExpertApprovalStatus.ChangesRequested;
        tenant.ReviewRequestNotes = notes.Trim();
        tenant.ExpertApprovalNotes = notes.Trim();
        tenant.ApprovedByUserId = expertUserId;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignReviewAsync(
        Guid tenantId,
        string expertUserId,
        AssignReviewRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var (tenant, group) = await RequireReviewableContextAsync(
            tenantId, expertUserId, ct, asPlatformAdmin, actAsGroupId);
        var assignee = string.IsNullOrWhiteSpace(request.AssigneeUserId)
            ? expertUserId
            : request.AssigneeUserId.Trim();

        var isSelfClaim = string.Equals(assignee, expertUserId, StringComparison.Ordinal);
        var canAssignOthers = asPlatformAdmin
            || managers.IsActiveManager(expertUserId, group.Id);
        if (!isSelfClaim && !canAssignOthers)
            throw new InvalidOperationException(
                "Seul le Responsable du groupe (ou un admin plateforme en mode suppléant) peut attribuer un dossier à un autre expert. Les experts peuvent uniquement s'auto-attribuer.");

        EnsureExpertInGroup(assignee, group.Id);

        tenant.ReviewAssignedToUserId = assignee;
        tenant.ReviewAssignedAt = DateTime.UtcNow;
        tenant.ReviewPriority = request.Urgent ? 1 : tenant.ReviewPriority;
        if (tenant.ExpertApprovalStatus == ExpertApprovalStatus.Pending)
            tenant.ExpertApprovalStatus = ExpertApprovalStatus.Assigned;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.CaseAssigned,
            expertUserId,
            $"Dossier « {tenant.Name} » attribué",
            group.Id,
            tenant.Id,
            ct: ct);
    }

    public async Task StartReviewAsync(
        Guid tenantId,
        string expertUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var (tenant, _) = await RequireReviewableContextAsync(
            tenantId, expertUserId, ct, asPlatformAdmin, actAsGroupId);
        tenant.ReviewAssignedToUserId ??= expertUserId;
        tenant.ReviewAssignedAt ??= DateTime.UtcNow;
        tenant.ExpertApprovalStatus = ExpertApprovalStatus.UnderReview;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ExpertApprovalQueueItemDto>> ListQueueForExpertAsync(
        string expertUserId,
        ExpertApprovalQueueFilter? filter = null,
        CancellationToken ct = default,
        Guid? overrideGroupId = null)
    {
        filter ??= new ExpertApprovalQueueFilter();
        HashSet<Guid> groupIds;
        if (overrideGroupId is Guid og)
            groupIds = [og];
        else
        {
            groupIds = db.ExpertGroupMembers
                .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
                .Select(m => m.ExpertGroupId)
                .Distinct()
                .ToHashSet();
        }
        if (groupIds.Count == 0)
            return Task.FromResult<IReadOnlyList<ExpertApprovalQueueItemDto>>([]);

        var statuses = filter.Status is ExpertApprovalStatus s
            ? new HashSet<ExpertApprovalStatus> { s }
            : new HashSet<ExpertApprovalStatus>
            {
                ExpertApprovalStatus.Pending,
                ExpertApprovalStatus.Assigned,
                ExpertApprovalStatus.UnderReview,
                ExpertApprovalStatus.ChangesRequested
            };

        var tenants = db.Tenants
            .Where(t => statuses.Contains(t.ExpertApprovalStatus))
            .OrderByDescending(t => t.ReviewPriority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var docCounts = db.TeacherDocumentsForAnyTenant
            .Where(d => tenants.Select(t => t.Id).Contains(d.TenantId))
            .GroupBy(d => d.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        var now = DateTime.UtcNow;
        var result = new List<ExpertApprovalQueueItemDto>();
        foreach (var t in tenants)
        {
            var suggested = t.ApprovedByExpertGroupId is Guid bound && groupIds.Contains(bound)
                ? db.ExpertGroups.FirstOrDefault(g => g.Id == bound)
                : expertGroups.ResolveReviewerGroup(t.Country);
            if (suggested is null || !groupIds.Contains(suggested.Id))
                continue;

            if (!string.IsNullOrWhiteSpace(filter.Country)
                && !string.Equals(t.Country, filter.Country, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(filter.City)
                && (t.City is null || !t.City.Contains(filter.City, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!string.IsNullOrWhiteSpace(filter.AssignedToUserId)
                && !string.Equals(t.ReviewAssignedToUserId, filter.AssignedToUserId, StringComparison.Ordinal))
                continue;
            if (filter.UrgentOnly == true && t.ReviewPriority < 1)
                continue;

            var docs = docCounts.GetValueOrDefault(t.Id);
            if (filter.MinDocuments is int min && docs < min)
                continue;
            var complete = docs >= 1;
            if (filter.IncompleteOnly == true && complete)
                continue;

            var age = (int)(now - t.CreatedAt).TotalDays;
            if (filter.OlderThanDays is int days && age < days)
                continue;

            result.Add(new ExpertApprovalQueueItemDto(
                t.Id, t.Name, t.Slug, t.Country, t.City, t.ExpertApprovalStatus, t.CreatedAt, age,
                null, null, docs, complete, t.ReviewPriority, t.ReviewAssignedToUserId, null,
                t.ReviewRequestNotes));
        }

        return Task.FromResult<IReadOnlyList<ExpertApprovalQueueItemDto>>(result);
    }

    public async Task<IReadOnlyList<TeacherDecisionItemDto>> ListRecentDecisionsAsync(
        string expertUserId,
        DateTime sinceUtc,
        CancellationToken ct = default,
        Guid? overrideGroupId = null)
    {
        HashSet<Guid> groupIds;
        if (overrideGroupId is Guid og)
            groupIds = [og];
        else
        {
            groupIds = db.ExpertGroupMembers
                .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
                .Select(m => m.ExpertGroupId)
                .Distinct()
                .ToHashSet();
        }
        if (groupIds.Count == 0)
            return [];

        var decided = db.Tenants
            .Where(t => t.ApprovedByExpertGroupId.HasValue
                        && groupIds.Contains(t.ApprovedByExpertGroupId.Value)
                        && (t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                            || t.ExpertApprovalStatus == ExpertApprovalStatus.Rejected)
                        && t.ExpertApprovedAt != null
                        && t.ExpertApprovedAt >= sinceUtc)
            .OrderByDescending(t => t.ExpertApprovedAt)
            .ToList();

        var list = new List<TeacherDecisionItemDto>(decided.Count);
        foreach (var t in decided)
        {
            string? email = null;
            string? name = null;
            if (!string.IsNullOrWhiteSpace(t.OwnerUserId))
            {
                var contact = await contacts.GetAsync(t.OwnerUserId, ct);
                email = contact?.Email;
                name = contact?.DisplayName;
            }
            list.Add(new TeacherDecisionItemDto(
                t.Id,
                string.IsNullOrWhiteSpace(name) ? t.Name : name!,
                email,
                t.ExpertApprovalStatus,
                t.ExpertApprovedAt,
                t.ExpertApprovalNotes));
        }
        return list;
    }

    public async Task InviteTeacherApplicationAsync(
        string expertUserId,
        InviteTeacherApplicationRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var toEmail = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@', StringComparison.Ordinal))
            throw new InvalidOperationException("Adresse e-mail invalide.");

        Guid membership;
        if (asPlatformAdmin && actAsGroupId is Guid actGid)
        {
            membership = actGid;
        }
        else
        {
            membership = db.ExpertGroupMembers
                .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
                .Select(m => m.ExpertGroupId)
                .FirstOrDefault();
        }
        if (membership == Guid.Empty)
            throw new InvalidOperationException("Vous n'êtes membre d'aucun groupe d'experts.");

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == membership && g.IsActive)
            ?? db.ExpertGroups.FirstOrDefault(g => g.Id == membership)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var expertContact = await contacts.GetAsync(expertUserId, ct);
        var expertName = string.IsNullOrWhiteSpace(expertContact?.DisplayName)
            ? "un expert TutorSphere"
            : expertContact!.Value.DisplayName;

        var firstName = string.IsNullOrWhiteSpace(request.FirstName)
            ? toEmail.Split('@')[0]
            : request.FirstName.Trim();

        var token = Guid.NewGuid().ToString("N");
        var invite = new TeacherApplicationInvite
        {
            Email = toEmail,
            FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim(),
            PersonalMessage = string.IsNullOrWhiteSpace(request.PersonalMessage)
                ? null
                : request.PersonalMessage.Trim(),
            InvitedByUserId = expertUserId,
            ExpertGroupId = group.Id,
            Token = token,
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = TeacherApplicationInviteStatus.Sent
        };
        db.Add(invite);
        await db.SaveChangesAsync(ct);

        var applyUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/tutor/apply?invite={Uri.EscapeDataString(token)}";

        await email.SendExpertTeacherApplyInviteAsync(
            toEmail,
            firstName,
            expertName,
            group.Name,
            request.PersonalMessage ?? "",
            applyUrl,
            ct);
    }

    public async Task<IReadOnlyList<TeacherApplicationInviteDto>> ListInvitesForExpertAsync(
        string expertUserId,
        CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToList();

        if (groupIds.Count == 0)
            return [];

        var invites = db.TeacherApplicationInvites
            .Where(i => groupIds.Contains(i.ExpertGroupId))
            .OrderByDescending(i => i.SentAt)
            .Take(200)
            .ToList();

        await RefreshInviteStatusesAsync(invites, ct);

        var groupNames = db.ExpertGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionary(g => g.Id, g => g.Name);

        var tenantIds = invites
            .Where(i => i.AcceptedTenantId is not null)
            .Select(i => i.AcceptedTenantId!.Value)
            .Distinct()
            .ToList();
        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id);

        var result = new List<TeacherApplicationInviteDto>(invites.Count);
        foreach (var invite in invites)
        {
            string? schoolName = null;
            if (invite.AcceptedTenantId is Guid tid && tenants.TryGetValue(tid, out var tenant))
                schoolName = tenant.Name;

            var inviter = await contacts.GetAsync(invite.InvitedByUserId, ct);
            result.Add(new TeacherApplicationInviteDto(
                invite.Id,
                invite.Email,
                invite.FirstName,
                invite.Status,
                invite.SentAt,
                invite.ExpiresAt,
                invite.AcceptedAt,
                invite.AcceptedTenantId,
                invite.InvitedByUserId,
                inviter?.DisplayName,
                invite.ExpertGroupId,
                groupNames.GetValueOrDefault(invite.ExpertGroupId),
                schoolName));
        }

        return result;
    }

    public async Task MarkInviteAcceptedAsync(
        string email,
        Guid tenantId,
        string? inviteToken = null,
        CancellationToken ct = default)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        TeacherApplicationInvite? invite = null;

        if (!string.IsNullOrWhiteSpace(inviteToken))
        {
            invite = db.TeacherApplicationInvites
                .FirstOrDefault(i => i.Token == inviteToken.Trim());
        }

        if (invite is null && !string.IsNullOrWhiteSpace(normalized))
        {
            invite = db.TeacherApplicationInvites
                .Where(i => i.Email == normalized
                            && i.Status == TeacherApplicationInviteStatus.Sent)
                .OrderByDescending(i => i.SentAt)
                .FirstOrDefault();
        }

        if (invite is null)
            return;

        invite.AcceptedTenantId = tenantId;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.Status = TeacherApplicationInviteStatus.Registered;
        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SyncInviteStatusForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null) return;

        var invites = db.TeacherApplicationInvites
            .Where(i => i.AcceptedTenantId == tenantId)
            .ToList();

        if (invites.Count == 0)
            return;

        var status = tenant.ExpertApprovalStatus switch
        {
            ExpertApprovalStatus.Approved => TeacherApplicationInviteStatus.Approved,
            ExpertApprovalStatus.Rejected => TeacherApplicationInviteStatus.Rejected,
            _ => TeacherApplicationInviteStatus.Registered
        };

        var changed = false;
        foreach (var invite in invites)
        {
            if (invite.Status == status) continue;
            invite.Status = status;
            invite.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private async Task RefreshInviteStatusesAsync(
        List<TeacherApplicationInvite> invites,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var invite in invites)
        {
            if (invite.Status == TeacherApplicationInviteStatus.Sent
                && invite.ExpiresAt is DateTime exp
                && exp < now)
            {
                invite.Status = TeacherApplicationInviteStatus.Expired;
                invite.UpdatedAt = now;
                changed = true;
                continue;
            }

            if (invite.AcceptedTenantId is Guid tid)
            {
                var tenant = db.Tenants.FirstOrDefault(t => t.Id == tid);
                if (tenant is null) continue;
                var mapped = tenant.ExpertApprovalStatus switch
                {
                    ExpertApprovalStatus.Approved => TeacherApplicationInviteStatus.Approved,
                    ExpertApprovalStatus.Rejected => TeacherApplicationInviteStatus.Rejected,
                    _ => TeacherApplicationInviteStatus.Registered
                };
                if (invite.Status != mapped)
                {
                    invite.Status = mapped;
                    invite.UpdatedAt = now;
                    changed = true;
                }
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    public Task<TeacherApprovalStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
            ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");

        ExpertGroup? group = null;
        if (tenant.ApprovedByExpertGroupId is Guid gid)
            group = db.ExpertGroups.FirstOrDefault(g => g.Id == gid);

        return Task.FromResult(new TeacherApprovalStatusDto(
            tenant.ExpertApprovalStatus,
            tenant.ExpertApprovalNotes,
            tenant.ExpertApprovedAt,
            group?.Id,
            group?.Name,
            group?.LogoUrl));
    }

    private async Task NotifyTeacherDecisionAsync(Tenant tenant, string groupName, bool approved, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            logger.LogWarning("Décision expert {TenantId} sans OwnerUserId — e-mail non envoyé.", tenant.Id);
            return;
        }

        try
        {
            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is null || string.IsNullOrWhiteSpace(contact.Value.Email))
            {
                logger.LogWarning(
                    "Décision expert {TenantId} — propriétaire sans e-mail (user {UserId}).",
                    tenant.Id, tenant.OwnerUserId);
                return;
            }

            var firstName = contact.Value.DisplayName
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? contact.Value.DisplayName;
            var loginUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/login/tuteur";
            var notes = string.IsNullOrWhiteSpace(tenant.ExpertApprovalNotes)
                ? "—"
                : tenant.ExpertApprovalNotes!;

            if (approved)
            {
                await email.SendExpertTeacherApprovedAsync(
                    contact.Value.Email,
                    firstName,
                    tenant.Name,
                    groupName,
                    notes,
                    loginUrl,
                    ct);
            }
            else
            {
                await email.SendExpertTeacherRejectedAsync(
                    contact.Value.Email,
                    firstName,
                    tenant.Name,
                    groupName,
                    notes,
                    loginUrl,
                    ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Échec e-mail décision expert ({Approved}) pour {TenantId}.",
                approved ? "approuvé" : "rejeté", tenant.Id);
        }
    }

    private Task<(Tenant Tenant, ExpertGroup Group)> RequireReviewableContextAsync(
        Guid tenantId,
        string expertUserId,
        CancellationToken ct,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Profil introuvable.");

        if (tenant.ExpertApprovalStatus is not (
            ExpertApprovalStatus.Pending
            or ExpertApprovalStatus.Assigned
            or ExpertApprovalStatus.UnderReview
            or ExpertApprovalStatus.ChangesRequested))
            throw new InvalidOperationException("Cette fiche n'est plus en cours de revue.");

        var suggested = expertGroups.ResolveReviewerGroup(tenant.Country)
            ?? throw new InvalidOperationException(
                "Aucun groupe d'experts disponible pour ce pays (ni groupe international).");

        if (asPlatformAdmin && actAsGroupId is Guid actGid)
        {
            if (actGid != suggested.Id)
                throw new InvalidOperationException("Cette fiche n'appartient pas au groupe administré.");
            return Task.FromResult((tenant, suggested));
        }

        if (asPlatformAdmin)
            return Task.FromResult((tenant, suggested));

        EnsureExpertInGroup(expertUserId, suggested.Id);
        return Task.FromResult((tenant, suggested));
    }

    private void EnsureExpertInGroup(string expertUserId, Guid groupId)
    {
        if (!db.ExpertGroupMembers.Any(m =>
                m.UserId == expertUserId
                && m.ExpertGroupId == groupId
                && m.Status == ExpertMembershipStatus.Active))
            throw new InvalidOperationException("Vous n'êtes pas membre du groupe d'experts assigné.");
    }

    private static PendingTeacherDto MapPending(Tenant t, ExpertGroup? suggested, int docCount) =>
        new(t.Id, t.Name, t.Slug, t.Country, t.City, t.ExpertApprovalStatus, t.CreatedAt,
            null, null, docCount, suggested?.Id, suggested?.Name);

    private static TeacherDocumentDto MapDoc(TeacherDocument d) =>
        new(d.Id, d.TenantId, d.DocumentType, d.FileName, d.FileUrl, d.ContentType,
            d.FileSizeBytes, d.CreatedAt, d.Notes);
}
