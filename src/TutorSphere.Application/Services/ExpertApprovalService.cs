using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertApprovalService
{
    Task<IReadOnlyList<PendingTeacherDto>> ListPendingForExpertAsync(string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTeacherDto>> ListAllPendingAsync(CancellationToken ct = default);
    Task<TeacherReviewDetailDto?> GetReviewDetailAsync(Guid tenantId, CancellationToken ct = default);
    Task ApproveAsync(Guid tenantId, string expertUserId, string? notes, CancellationToken ct = default);
    Task RejectAsync(Guid tenantId, string expertUserId, string? notes, CancellationToken ct = default);
    Task InviteTeacherApplicationAsync(string expertUserId, InviteTeacherApplicationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherApplicationInviteDto>> ListInvitesForExpertAsync(string expertUserId, CancellationToken ct = default);
    Task MarkInviteAcceptedAsync(string email, Guid tenantId, string? inviteToken = null, CancellationToken ct = default);
    Task SyncInviteStatusForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TeacherApprovalStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetExpertGroupIdsAsync(string expertUserId, CancellationToken ct = default);
    Task<ExpertMyGroupDto?> GetMyGroupAsync(string expertUserId, CancellationToken ct = default);
}

public class ExpertApprovalService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls,
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
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
        if (groupId == Guid.Empty)
            return Task.FromResult<ExpertMyGroupDto?>(null);

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId && g.IsActive);
        if (group is null)
            return Task.FromResult<ExpertMyGroupDto?>(null);

        return Task.FromResult<ExpertMyGroupDto?>(
            new ExpertMyGroupDto(group.Id, group.Name, group.CountryCode));
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
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Pending)
            .OrderBy(t => t.CreatedAt)
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
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Pending)
            .OrderBy(t => t.CreatedAt)
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

    public async Task ApproveAsync(Guid tenantId, string expertUserId, string? notes, CancellationToken ct = default)
    {
        var tenant = await RequirePendingForExpertAsync(tenantId, expertUserId, ct);
        var group = expertGroups.ResolveReviewerGroup(tenant.Country)
            ?? throw new InvalidOperationException(
                "Aucun groupe d'experts disponible pour ce pays (ni groupe international).");

        EnsureExpertInGroup(expertUserId, group.Id);

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

    public async Task RejectAsync(Guid tenantId, string expertUserId, string? notes, CancellationToken ct = default)
    {
        var tenant = await RequirePendingForExpertAsync(tenantId, expertUserId, ct);
        var group = expertGroups.ResolveReviewerGroup(tenant.Country)
            ?? throw new InvalidOperationException(
                "Aucun groupe d'experts disponible pour ce pays (ni groupe international).");

        EnsureExpertInGroup(expertUserId, group.Id);

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

    public async Task InviteTeacherApplicationAsync(
        string expertUserId,
        InviteTeacherApplicationRequest request,
        CancellationToken ct = default)
    {
        var toEmail = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@', StringComparison.Ordinal))
            throw new InvalidOperationException("Adresse e-mail invalide.");

        var membership = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
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

        var applyUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/tutor/register?invite={Uri.EscapeDataString(token)}";

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

    private Task<Tenant> RequirePendingForExpertAsync(Guid tenantId, string expertUserId, CancellationToken ct)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École introuvable.");

        if (tenant.ExpertApprovalStatus != ExpertApprovalStatus.Pending)
            throw new InvalidOperationException("Cette fiche n'est plus en attente d'approbation.");

        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();
        if (groupIds.Count == 0)
            throw new InvalidOperationException("Vous n'êtes membre d'aucun groupe d'experts.");

        var suggested = expertGroups.ResolveReviewerGroup(tenant.Country);
        if (suggested is null || !groupIds.Contains(suggested.Id))
            throw new InvalidOperationException("Cette fiche n'est pas assignée à votre groupe d'experts.");

        return Task.FromResult(tenant);
    }

    private void EnsureExpertInGroup(string expertUserId, Guid groupId)
    {
        if (!db.ExpertGroupMembers.Any(m => m.UserId == expertUserId && m.ExpertGroupId == groupId))
            throw new InvalidOperationException("Vous n'êtes pas membre du groupe d'experts assigné.");
    }

    private static PendingTeacherDto MapPending(Tenant t, ExpertGroup? suggested, int docCount) =>
        new(t.Id, t.Name, t.Slug, t.Country, t.City, t.ExpertApprovalStatus, t.CreatedAt,
            null, null, docCount, suggested?.Id, suggested?.Name);

    private static TeacherDocumentDto MapDoc(TeacherDocument d) =>
        new(d.Id, d.TenantId, d.DocumentType, d.FileName, d.FileUrl, d.ContentType,
            d.FileSizeBytes, d.CreatedAt, d.Notes);
}
