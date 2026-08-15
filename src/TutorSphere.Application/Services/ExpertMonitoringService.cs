using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>Suivi post-approbation des enseignants par les experts : activité, supports de cours, remarques.</summary>
public interface IExpertMonitoringService
{
    Task<IReadOnlyList<MonitoredTeacherDto>> ListMonitoredTeachersAsync(string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null);
    Task<IReadOnlyList<TeacherDirectoryItemDto>> ListTeacherDirectoryAsync(string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null);
    Task<IReadOnlyList<TeacherMaterialItemDto>> GetTeacherMaterialsAsync(Guid tenantId, string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksAsync(Guid tenantId, string expertUserId, CancellationToken ct = default);
    Task<ExpertRemarkDto> AddRemarkAsync(string expertUserId, Guid tenantId, CreateExpertRemarkRequest request, CancellationToken ct = default);
    Task MarkRemarkReadAsync(Guid remarkId, string requestingOwnerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertRemarkDto>> ListRemarksForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    /// <summary>Vérifie que l'expert peut gérer l'enseignant (même groupe d'approbation).</summary>
    void EnsureCanMonitorTeacher(Guid tenantId, string expertUserId);
}

public class ExpertMonitoringService(
    IApplicationDbContext db,
    IUserContactLookup contacts,
    IEmailService email,
    IAppUrlProvider urls,
    IExpertGroupService expertGroups,
    ILogger<ExpertMonitoringService> logger) : IExpertMonitoringService
{
    private static readonly ExpertApprovalStatus[] PipelineStatuses =
    [
        ExpertApprovalStatus.Pending,
        ExpertApprovalStatus.Assigned,
        ExpertApprovalStatus.UnderReview,
        ExpertApprovalStatus.ChangesRequested
    ];
    public Task<IReadOnlyList<MonitoredTeacherDto>> ListMonitoredTeachersAsync(
        string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null)
    {
        var groupIds = overrideGroupId is Guid og
            ? new HashSet<Guid> { og }
            : GetExpertGroupIds(expertUserId);
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

    public async Task<IReadOnlyList<TeacherDirectoryItemDto>> ListTeacherDirectoryAsync(
        string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null)
    {
        Guid groupId;
        if (overrideGroupId is Guid og)
        {
            if (!db.ExpertGroups.Any(g => g.Id == og && g.IsActive))
                return [];
            groupId = og;
        }
        else
        {
            var membership = db.ExpertGroupMembers
                .FirstOrDefault(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active);
            if (membership is null)
                return [];
            var group = db.ExpertGroups.FirstOrDefault(g => g.Id == membership.ExpertGroupId && g.IsActive);
            if (group is null)
                return [];
            groupId = group.Id;
        }

        var roster = db.Tenants
            .Where(t =>
                (t.ApprovedByExpertGroupId == groupId
                 && (t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                     || t.ExpertApprovalStatus == ExpertApprovalStatus.Suspended))
                || PipelineStatuses.Contains(t.ExpertApprovalStatus))
            .ToList();

        var tenants = new List<Tenant>(roster.Count);
        var seen = new HashSet<Guid>();
        foreach (var t in roster)
        {
            if (t.ExpertApprovalStatus is ExpertApprovalStatus.Approved or ExpertApprovalStatus.Suspended)
            {
                if (t.ApprovedByExpertGroupId == groupId && seen.Add(t.Id))
                    tenants.Add(t);
                continue;
            }

            var suggested = expertGroups.ResolveReviewerGroup(t.Country);
            if (suggested is not null && suggested.Id == groupId && seen.Add(t.Id))
                tenants.Add(t);
        }

        if (tenants.Count == 0)
            return [];

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var brandings = db.TenantBrandings.Where(b => tenantIds.Contains(b.TenantId)).ToList()
            .ToDictionary(b => b.TenantId);
        var offerings = db.SubscriptionOfferingsForAnyTenant.Where(o => tenantIds.Contains(o.TenantId)).ToList();
        var assignments = db.TeacherDisciplineAssignments.Where(a => tenantIds.Contains(a.TenantId)).ToList();
        var disciplineIds = assignments.Select(a => a.DisciplineId).Distinct().ToList();
        var disciplines = disciplineIds.Count == 0
            ? new Dictionary<Guid, Discipline>()
            : db.Disciplines.Where(d => disciplineIds.Contains(d.Id)).ToList().ToDictionary(d => d.Id);

        var result = new List<TeacherDirectoryItemDto>(tenants.Count);
        foreach (var t in tenants.OrderBy(x => x.Name))
        {
            brandings.TryGetValue(t.Id, out var branding);
            var portfolio = ParseDirectoryPortfolio(branding?.Portfolio);
            var subjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var levels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in portfolio.Subjects)
                if (!string.IsNullOrWhiteSpace(s)) subjects.Add(s.Trim());
            foreach (var lv in portfolio.Levels)
                if (!string.IsNullOrWhiteSpace(lv)) levels.Add(lv.Trim());

            foreach (var o in offerings.Where(o => o.TenantId == t.Id))
            {
                if (!string.IsNullOrWhiteSpace(o.Subject))
                    subjects.Add(o.Subject.Trim());
                var level = ExtractLevel(o.Conditions);
                if (!string.IsNullOrWhiteSpace(level) && !IsAllLevels(level))
                    levels.Add(level.Trim());
            }

            foreach (var a in assignments.Where(a => a.TenantId == t.Id))
            {
                if (!disciplines.TryGetValue(a.DisciplineId, out var d) || !d.IsActive)
                    continue;
                subjects.Add(d.Name.Trim());
                var cycle = CycleLabel(d.Cycle);
                if (cycle is not null)
                    levels.Add(cycle);
            }

            string? email = null;
            string? name = null;
            if (!string.IsNullOrWhiteSpace(t.OwnerUserId))
            {
                var contact = await contacts.GetAsync(t.OwnerUserId, ct);
                email = contact?.Email;
                name = contact?.DisplayName;
            }

            var display = string.IsNullOrWhiteSpace(name) ? t.Name : name!;
            result.Add(new TeacherDirectoryItemDto(
                t.Id,
                display,
                email,
                branding?.LogoUrl,
                subjects.OrderBy(s => s).ToList(),
                levels.OrderBy(s => s).ToList(),
                portfolio.YearsExperience,
                t.ExpertApprovalStatus,
                t.CreatedAt,
                t.Slug,
                t.IsPublicProfile,
                t.City,
                t.Country));
        }

        return result;
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
            ?? throw new InvalidOperationException("Profil introuvable.");

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

    public void EnsureCanMonitorTeacher(Guid tenantId, string expertUserId)
        => EnsureCanMonitor(tenantId, expertUserId);

    private HashSet<Guid> GetExpertGroupIds(string expertUserId) =>
        db.ExpertGroupMembers.Where(m => m.UserId == expertUserId).Select(m => m.ExpertGroupId).ToHashSet();

    private Guid EnsureCanMonitor(Guid tenantId, string expertUserId, Tenant? tenant = null)
    {
        tenant ??= db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Profil introuvable.");

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

    private static string? CycleLabel(SchoolCycle cycle) => cycle switch
    {
        SchoolCycle.Primary => "Primaire",
        SchoolCycle.Secondary => "Secondaire",
        SchoolCycle.University => "Université",
        SchoolCycle.AdultEducation => "Adultes",
        _ => null
    };

    private static bool IsAllLevels(string? level) =>
        string.Equals(level?.Trim(), "Tous niveaux", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractLevel(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(conditions);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;
            foreach (var name in new[] { "level", "Level" })
            {
                if (root.TryGetProperty(name, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var v = el.GetString();
                    return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                }
            }
        }
        catch (System.Text.Json.JsonException) { }
        return null;
    }

    private static (int? YearsExperience, IReadOnlyList<string> Subjects, IReadOnlyList<string> Levels)
        ParseDirectoryPortfolio(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, [], []);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            int? years = null;
            foreach (var name in new[] { "yearsExperience", "YearsExperience" })
            {
                if (!root.TryGetProperty(name, out var el))
                    continue;
                if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n))
                    years = n;
                break;
            }

            return (years, ReadStringList(root, "subjects", "Subjects"), ReadStringList(root, "levels", "Levels"));
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, [], []);
        }
    }

    private static IReadOnlyList<string> ReadStringList(System.Text.Json.JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != System.Text.Json.JsonValueKind.Array)
                continue;
            var list = new List<string>();
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.String)
                    continue;
                var v = item.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                    list.Add(v.Trim());
            }
            return list;
        }
        return [];
    }
}
