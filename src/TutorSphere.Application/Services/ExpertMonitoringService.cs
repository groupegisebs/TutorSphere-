using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>Suivi post-approbation des enseignants par les experts : activité, supports de cours, remarques.</summary>
public interface IExpertMonitoringService
{
    Task<IReadOnlyList<MonitoredTeacherDto>> ListMonitoredTeachersAsync(string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherMaterialItemDto>> GetTeacherMaterialsAsync(Guid tenantId, string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksAsync(Guid tenantId, string expertUserId, CancellationToken ct = default);
    Task<ExpertRemarkDto> AddRemarkAsync(string expertUserId, Guid tenantId, CreateExpertRemarkRequest request, CancellationToken ct = default);
    Task MarkRemarkReadAsync(Guid remarkId, string requestingOwnerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksForOwnerAsync(string ownerUserId, CancellationToken ct = default);
}

public class ExpertMonitoringService(
    IApplicationDbContext db,
    IUserContactLookup contacts,
    IEmailService email,
    IAppUrlProvider urls,
    ILogger<ExpertMonitoringService> logger) : IExpertMonitoringService
{
    public Task<IReadOnlyList<MonitoredTeacherDto>> ListMonitoredTeachersAsync(string expertUserId, CancellationToken ct = default)
    {
        var groupIds = GetExpertGroupIds(expertUserId);
        if (groupIds.Count == 0)
            return Task.FromResult<IReadOnlyList<MonitoredTeacherDto>>([]);

        var tenants = db.Tenants
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.ApprovedByExpertGroupId.HasValue
                        && groupIds.Contains(t.ApprovedByExpertGroupId.Value))
            .OrderBy(t => t.Name)
            .ToList();
        if (tenants.Count == 0)
            return Task.FromResult<IReadOnlyList<MonitoredTeacherDto>>([]);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var lessonStats = db.LessonsForAnyTenant
            .Where(l => tenantIds.Contains(l.TenantId))
            .GroupBy(l => l.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Total = g.Count(l => l.SettlementStatus == LessonSettlementStatus.Validated
                                      || l.SettlementStatus == LessonSettlementStatus.LiabilityResolved),
                Cancelled = g.Count(l => l.SettlementStatus == LessonSettlementStatus.CancelledFree),
                NoShow = g.Count(l => l.SettlementStatus == LessonSettlementStatus.TutorNoShow
                                       || l.SettlementStatus == LessonSettlementStatus.LiabilityResolved),
                LastActivity = g.Max(l => (DateTime?)l.StartTime)
            })
            .ToList()
            .ToDictionary(x => x.TenantId);

        var remarkStats = db.ExpertRemarksForAnyTenant
            .Where(r => tenantIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count(), Last = g.Max(r => (DateTime?)r.CreatedAt) })
            .ToList()
            .ToDictionary(x => x.TenantId);

        var result = new List<MonitoredTeacherDto>(tenants.Count);
        foreach (var t in tenants)
        {
            lessonStats.TryGetValue(t.Id, out var ls);
            remarkStats.TryGetValue(t.Id, out var rs);
            result.Add(new MonitoredTeacherDto(
                t.Id, t.Name, t.Country, t.City, null, null,
                ls?.Total ?? 0, ls?.Cancelled ?? 0, ls?.NoShow ?? 0, ls?.LastActivity,
                rs?.Count ?? 0, rs?.Last));
        }

        return Task.FromResult<IReadOnlyList<MonitoredTeacherDto>>(result);
    }

    public Task<IReadOnlyList<TeacherMaterialItemDto>> GetTeacherMaterialsAsync(
        Guid tenantId, string expertUserId, CancellationToken ct = default)
    {
        EnsureCanMonitor(tenantId, expertUserId);

        var homeworks = db.HomeworksForAnyTenant.Where(h => h.TenantId == tenantId).ToList();
        var documents = db.DocumentsForAnyTenant.Where(d => d.TenantId == tenantId).ToList();

        var relatedRemarks = db.ExpertRemarksForAnyTenant
            .Where(r => r.TenantId == tenantId && (r.RelatedHomeworkId != null || r.RelatedDocumentId != null))
            .ToList();

        var items = new List<TeacherMaterialItemDto>(homeworks.Count + documents.Count);
        foreach (var h in homeworks)
        {
            var count = relatedRemarks.Count(r => r.RelatedHomeworkId == h.Id);
            items.Add(new TeacherMaterialItemDto(h.Id, "Homework", h.Title, h.Subject, null, h.CreatedAt, count));
        }
        foreach (var d in documents)
        {
            var count = relatedRemarks.Count(r => r.RelatedDocumentId == d.Id);
            items.Add(new TeacherMaterialItemDto(d.Id, "Document", d.Name, d.Folder, d.FileUrl, d.CreatedAt, count));
        }

        IReadOnlyList<TeacherMaterialItemDto> ordered = items.OrderByDescending(i => i.CreatedAt).ToList();
        return Task.FromResult(ordered);
    }

    public async Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksAsync(
        Guid tenantId, string expertUserId, CancellationToken ct = default)
    {
        EnsureCanMonitor(tenantId, expertUserId);
        return await MapRemarksAsync(tenantId, ct);
    }

    public async Task<ExpertRemarkDto> AddRemarkAsync(
        string expertUserId, Guid tenantId, CreateExpertRemarkRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Le message de la remarque est requis.");

        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École introuvable.");

        var groupId = EnsureCanMonitor(tenantId, expertUserId, tenant);

        var remark = new ExpertRemark
        {
            TenantId = tenantId,
            ExpertGroupId = groupId,
            AuthorUserId = expertUserId,
            Category = request.Category,
            Message = request.Message.Trim(),
            RelatedHomeworkId = request.RelatedHomeworkId,
            RelatedDocumentId = request.RelatedDocumentId
        };
        db.Add(remark);
        await db.SaveChangesAsync(ct);

        await NotifyTeacherOfRemarkAsync(tenant, remark, ct);

        var author = await contacts.GetAsync(expertUserId, ct);
        return new ExpertRemarkDto(
            remark.Id, remark.TenantId, remark.Category, remark.Message,
            remark.RelatedHomeworkId, remark.RelatedDocumentId,
            remark.AuthorUserId, author?.DisplayName, remark.CreatedAt, remark.ReadByTeacherAt);
    }

    public async Task MarkRemarkReadAsync(
        Guid remarkId, string requestingOwnerUserId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == requestingOwnerUserId)
            ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");

        var remark = db.ExpertRemarksForAnyTenant.FirstOrDefault(r => r.Id == remarkId && r.TenantId == tenant.Id)
            ?? throw new InvalidOperationException("Remarque introuvable.");

        if (remark.ReadByTeacherAt is null)
        {
            remark.ReadByTeacherAt = DateTime.UtcNow;
            remark.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksForOwnerAsync(
        string ownerUserId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
            ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");

        return await MapRemarksAsync(tenant.Id, ct);
    }

    private async Task<IReadOnlyList<ExpertRemarkDto>> MapRemarksAsync(Guid tenantId, CancellationToken ct)
    {
        var remarks = db.ExpertRemarksForAnyTenant
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var result = new List<ExpertRemarkDto>(remarks.Count);
        foreach (var r in remarks)
        {
            var author = await contacts.GetAsync(r.AuthorUserId, ct);
            result.Add(new ExpertRemarkDto(
                r.Id, r.TenantId, r.Category, r.Message, r.RelatedHomeworkId, r.RelatedDocumentId,
                r.AuthorUserId, author?.DisplayName, r.CreatedAt, r.ReadByTeacherAt));
        }
        return result;
    }

    private HashSet<Guid> GetExpertGroupIds(string expertUserId) =>
        db.ExpertGroupMembers.Where(m => m.UserId == expertUserId).Select(m => m.ExpertGroupId).ToHashSet();

    private Guid EnsureCanMonitor(Guid tenantId, string expertUserId, Tenant? tenant = null)
    {
        tenant ??= db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École introuvable.");

        if (tenant.ExpertApprovalStatus != ExpertApprovalStatus.Approved || tenant.ApprovedByExpertGroupId is not Guid groupId)
            throw new InvalidOperationException("Cet enseignant n'est pas (ou plus) approuvé par un groupe d'experts.");

        var isMember = db.ExpertGroupMembers.Any(m => m.UserId == expertUserId && m.ExpertGroupId == groupId);
        if (!isMember)
            throw new InvalidOperationException("Vous n'êtes pas membre du groupe d'experts responsable de cet enseignant.");

        return groupId;
    }

    private async Task NotifyTeacherOfRemarkAsync(Tenant tenant, ExpertRemark remark, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            logger.LogWarning("Remarque expert {TenantId} sans OwnerUserId — e-mail non envoyé.", tenant.Id);
            return;
        }

        try
        {
            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is null || string.IsNullOrWhiteSpace(contact.Value.Email))
            {
                logger.LogWarning(
                    "Remarque expert {TenantId} — propriétaire sans e-mail (user {UserId}).",
                    tenant.Id, tenant.OwnerUserId);
                return;
            }

            var firstName = contact.Value.DisplayName
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? contact.Value.DisplayName;
            var remarksUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/tutor/expert-remarks";
            var excerpt = remark.Message.Length > 200 ? remark.Message[..200] + "…" : remark.Message;

            await email.SendExpertRemarkNotificationAsync(
                contact.Value.Email, firstName, tenant.Name, CategoryLabel(remark.Category), excerpt, remarksUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec e-mail notification remarque expert pour tenant {TenantId}.", tenant.Id);
        }
    }

    private static string CategoryLabel(ExpertRemarkCategory category) => category switch
    {
        ExpertRemarkCategory.Activity => "Activité",
        ExpertRemarkCategory.CourseMaterial => "Matériel pédagogique",
        _ => "Général"
    };
}
