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
    Task<TeacherApprovalStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetExpertGroupIdsAsync(string expertUserId, CancellationToken ct = default);
}

public class ExpertApprovalService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups) : IExpertApprovalService
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
    }

    public async Task RejectAsync(Guid tenantId, string expertUserId, string? notes, CancellationToken ct = default)
    {
        var tenant = await RequirePendingForExpertAsync(tenantId, expertUserId, ct);
        var group = expertGroups.ResolveReviewerGroup(tenant.Country)
            ?? throw new InvalidOperationException(
                "Aucun groupe d'experts disponible pour ce pays (ni groupe international).");

        EnsureExpertInGroup(expertUserId, group.Id);

        tenant.ExpertApprovalStatus = ExpertApprovalStatus.Rejected;
        tenant.ApprovedByExpertGroupId = group.Id;
        tenant.ApprovedByUserId = expertUserId;
        tenant.ExpertApprovedAt = DateTime.UtcNow;
        tenant.ExpertApprovalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        tenant.ExpertReviewNotifiedAt = null; // permet une nouvelle alerte si repasse en Pending
        tenant.IsPublicProfile = false;
        tenant.UpdatedAt = DateTime.UtcNow;

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
