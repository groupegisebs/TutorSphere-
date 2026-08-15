using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>
/// Gestion, par un groupe d'experts, des disciplines qu'il définit (par cycle scolaire),
/// des services détaillés associés, et de l'affectation des enseignants de son groupe à ces disciplines.
/// </summary>
public interface IExpertDisciplineService
{
    Task<IReadOnlyList<DisciplineDto>> ListForExpertAsync(
        string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null);
    Task<DisciplineDto> GetByIdAsync(Guid disciplineId, string expertUserId, CancellationToken ct = default);
    Task<DisciplineDto> CreateAsync(string expertUserId, CreateDisciplineRequest request, CancellationToken ct = default);
    Task<DisciplineDto> UpdateAsync(Guid disciplineId, string expertUserId, UpdateDisciplineRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid disciplineId, string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupTeacherAssignmentDto>> ListGroupTeachersAsync(Guid disciplineId, string expertUserId, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherDisciplineStatusDto>> ListAssignmentsForTeacherAsync(Guid tenantId, string expertUserId, CancellationToken ct = default);
    Task AssignTeacherAsync(Guid disciplineId, string expertUserId, Guid tenantId, CancellationToken ct = default);
    Task UnassignTeacherAsync(Guid disciplineId, string expertUserId, Guid tenantId, CancellationToken ct = default);
}

public class ExpertDisciplineService(IApplicationDbContext db, IUserContactLookup contacts) : IExpertDisciplineService
{
    public async Task<IReadOnlyList<DisciplineDto>> ListForExpertAsync(
        string expertUserId, CancellationToken ct = default, Guid? overrideGroupId = null)
    {
        var groupId = overrideGroupId is Guid og && og != Guid.Empty
            ? og
            : GetExpertGroupId(expertUserId);

        var disciplines = db.Disciplines
            .Where(d => d.ExpertGroupId == groupId)
            .OrderBy(d => d.Cycle).ThenBy(d => d.Name)
            .ToList();
        if (disciplines.Count == 0)
            return [];

        var ids = disciplines.Select(d => d.Id).ToList();
        var services = db.DisciplineServiceItems.Where(s => ids.Contains(s.DisciplineId))
            .OrderBy(s => s.SortOrder).ToList();
        var assignedCounts = db.TeacherDisciplineAssignments.Where(a => ids.Contains(a.DisciplineId))
            .GroupBy(a => a.DisciplineId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.Key, x => x.Count);

        await Task.CompletedTask;
        return disciplines
            .Select(d => Map(d, services.Where(s => s.DisciplineId == d.Id).ToList(), assignedCounts.GetValueOrDefault(d.Id)))
            .ToList();
    }

    public async Task<DisciplineDto> GetByIdAsync(Guid disciplineId, string expertUserId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        var discipline = GetOwnedDiscipline(disciplineId, groupId);
        var services = db.DisciplineServiceItems.Where(s => s.DisciplineId == disciplineId).OrderBy(s => s.SortOrder).ToList();
        var count = db.TeacherDisciplineAssignments.Count(a => a.DisciplineId == disciplineId);
        await Task.CompletedTask;
        return Map(discipline, services, count);
    }

    public async Task<DisciplineDto> CreateAsync(string expertUserId, CreateDisciplineRequest request, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom de la discipline est requis.");
        if (db.Disciplines.Any(d => d.ExpertGroupId == groupId && d.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Une discipline « {name} » existe déjà pour votre groupe.");

        var entity = new Discipline
        {
            ExpertGroupId = groupId,
            Name = name,
            Cycle = request.Cycle,
            WorkMethod = TrimOrNull(request.WorkMethod),
            IsActive = true
        };
        db.Add(entity);

        var services = BuildServiceItems(request.Services, entity.Id);
        foreach (var s in services)
            db.Add(s);

        await db.SaveChangesAsync(ct);

        return Map(entity, services, 0);
    }

    public async Task<DisciplineDto> UpdateAsync(
        Guid disciplineId, string expertUserId, UpdateDisciplineRequest request, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        var entity = GetOwnedDiscipline(disciplineId, groupId);

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom de la discipline est requis.");
        if (db.Disciplines.Any(d => d.ExpertGroupId == groupId && d.Id != disciplineId && d.Name.ToLower() == name.ToLower()))
            throw new InvalidOperationException($"Une discipline « {name} » existe déjà pour votre groupe.");

        entity.Name = name;
        entity.Cycle = request.Cycle;
        entity.WorkMethod = TrimOrNull(request.WorkMethod);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        var existing = db.DisciplineServiceItems.Where(s => s.DisciplineId == disciplineId).ToList();
        var incoming = (request.Services ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
        var incomingIds = incoming.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToHashSet();

        foreach (var old in existing.Where(e => !incomingIds.Contains(e.Id)))
            db.Remove(old);

        var finalItems = new List<DisciplineServiceItem>();
        var sort = 0;
        foreach (var s in incoming)
        {
            var order = s.SortOrder != 0 ? s.SortOrder : sort;
            if (s.Id.HasValue)
            {
                var match = existing.FirstOrDefault(e => e.Id == s.Id.Value);
                if (match is not null)
                {
                    match.Title = s.Title.Trim();
                    match.Description = TrimOrNull(s.Description);
                    match.SortOrder = order;
                    match.UpdatedAt = DateTime.UtcNow;
                    finalItems.Add(match);
                    sort++;
                    continue;
                }
            }

            var created = new DisciplineServiceItem
            {
                DisciplineId = disciplineId,
                Title = s.Title.Trim(),
                Description = TrimOrNull(s.Description),
                SortOrder = order
            };
            db.Add(created);
            finalItems.Add(created);
            sort++;
        }

        await db.SaveChangesAsync(ct);

        var count = db.TeacherDisciplineAssignments.Count(a => a.DisciplineId == disciplineId);
        return Map(entity, finalItems.OrderBy(i => i.SortOrder).ToList(), count);
    }

    public async Task DeleteAsync(Guid disciplineId, string expertUserId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        var entity = GetOwnedDiscipline(disciplineId, groupId);

        if (db.TeacherDisciplineAssignments.Any(a => a.DisciplineId == disciplineId))
            throw new InvalidOperationException(
                "Impossible de supprimer : des enseignants sont affectés à cette discipline. Désactivez-la plutôt, ou retirez d'abord les enseignants affectés.");

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GroupTeacherAssignmentDto>> ListGroupTeachersAsync(
        Guid disciplineId, string expertUserId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        GetOwnedDiscipline(disciplineId, groupId);

        var teachers = db.Tenants
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Approved && t.ApprovedByExpertGroupId == groupId)
            .OrderBy(t => t.Name)
            .ToList();
        if (teachers.Count == 0)
            return [];

        var assignedIds = db.TeacherDisciplineAssignments
            .Where(a => a.DisciplineId == disciplineId)
            .Select(a => a.TenantId)
            .ToHashSet();

        var result = new List<GroupTeacherAssignmentDto>(teachers.Count);
        foreach (var t in teachers)
        {
            (string Email, string DisplayName)? contact = string.IsNullOrWhiteSpace(t.OwnerUserId)
                ? null
                : await contacts.GetAsync(t.OwnerUserId, ct);
            result.Add(new GroupTeacherAssignmentDto(
                t.Id, t.Name, contact?.Email, contact?.DisplayName, assignedIds.Contains(t.Id)));
        }
        return result;
    }

    public Task<IReadOnlyList<TeacherDisciplineStatusDto>> ListAssignmentsForTeacherAsync(
        Guid tenantId, string expertUserId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");
        if (tenant.ApprovedByExpertGroupId != groupId
            && tenant.ExpertApprovalStatus == ExpertApprovalStatus.Approved)
            throw new InvalidOperationException("Cet enseignant n'appartient pas à votre groupe d'experts.");

        var assigned = db.TeacherDisciplineAssignments
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.DisciplineId)
            .ToHashSet();

        var list = db.Disciplines
            .Where(d => d.ExpertGroupId == groupId)
            .OrderBy(d => d.Name)
            .AsEnumerable()
            .Select(d => new TeacherDisciplineStatusDto(
                d.Id, d.Name, d.Cycle, d.IsActive, assigned.Contains(d.Id)))
            .ToList();

        return Task.FromResult<IReadOnlyList<TeacherDisciplineStatusDto>>(list);
    }

    public async Task AssignTeacherAsync(Guid disciplineId, string expertUserId, Guid tenantId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        GetOwnedDiscipline(disciplineId, groupId);

        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");
        if (tenant.ExpertApprovalStatus != ExpertApprovalStatus.Approved || tenant.ApprovedByExpertGroupId != groupId)
            throw new InvalidOperationException("Cet enseignant n'appartient pas à votre groupe d'experts.");

        if (db.TeacherDisciplineAssignments.Any(a => a.DisciplineId == disciplineId && a.TenantId == tenantId))
            return;

        db.Add(new TeacherDisciplineAssignment
        {
            DisciplineId = disciplineId,
            TenantId = tenantId,
            AssignedByUserId = expertUserId
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task UnassignTeacherAsync(Guid disciplineId, string expertUserId, Guid tenantId, CancellationToken ct = default)
    {
        var groupId = GetExpertGroupId(expertUserId);
        GetOwnedDiscipline(disciplineId, groupId);

        var assignment = db.TeacherDisciplineAssignments
            .FirstOrDefault(a => a.DisciplineId == disciplineId && a.TenantId == tenantId);
        if (assignment is null)
            return;

        db.Remove(assignment);
        await db.SaveChangesAsync(ct);
    }

    private Guid GetExpertGroupId(string expertUserId)
    {
        var groupId = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
        if (groupId == Guid.Empty)
        {
            groupId = db.ExpertGroupMembers
                .Where(m => m.UserId == expertUserId)
                .Select(m => m.ExpertGroupId)
                .FirstOrDefault();
        }
        if (groupId == Guid.Empty)
            throw new InvalidOperationException("Vous n'êtes membre d'aucun groupe d'experts.");
        return groupId;
    }

    private Discipline GetOwnedDiscipline(Guid disciplineId, Guid groupId)
    {
        var discipline = db.Disciplines.FirstOrDefault(d => d.Id == disciplineId)
            ?? throw new InvalidOperationException("Discipline introuvable.");
        if (discipline.ExpertGroupId != groupId)
            throw new InvalidOperationException("Cette discipline n'appartient pas à votre groupe d'experts.");
        return discipline;
    }

    private static List<DisciplineServiceItem> BuildServiceItems(
        IReadOnlyList<DisciplineServiceItemInput>? inputs, Guid disciplineId)
    {
        if (inputs is null || inputs.Count == 0)
            return [];

        var sort = 0;
        var result = new List<DisciplineServiceItem>();
        foreach (var s in inputs)
        {
            if (string.IsNullOrWhiteSpace(s.Title))
                continue;
            result.Add(new DisciplineServiceItem
            {
                DisciplineId = disciplineId,
                Title = s.Title.Trim(),
                Description = TrimOrNull(s.Description),
                SortOrder = s.SortOrder != 0 ? s.SortOrder : sort
            });
            sort++;
        }
        return result;
    }

    private static DisciplineDto Map(Discipline d, IReadOnlyList<DisciplineServiceItem> services, int assignedCount) =>
        new(d.Id, d.ExpertGroupId, d.Name, d.Cycle, d.WorkMethod, d.IsActive,
            services.Select(s => new DisciplineServiceItemDto(s.Id, s.Title, s.Description, s.SortOrder)).ToList(),
            assignedCount, d.CreatedAt);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
