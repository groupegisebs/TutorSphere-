using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertDelegatedTaskService
{
    Task<IReadOnlyList<ExpertDelegatedTaskDto>> ListForManagerAsync(string managerUserId, CancellationToken ct = default, Guid? overrideGroupId = null);
    Task<IReadOnlyList<ExpertDelegatedTaskDto>> ListForAssigneeAsync(string expertUserId, CancellationToken ct = default);
    Task<ExpertDelegatedTaskDto> CreateAsync(string managerUserId, CreateExpertDelegatedTaskRequest request, CancellationToken ct = default, Guid? overrideGroupId = null);
    Task<ExpertDelegatedTaskDto> StartAsync(Guid taskId, string expertUserId, CancellationToken ct = default);
    Task<ExpertDelegatedTaskDto> CompleteAsync(Guid taskId, string expertUserId, CompleteExpertDelegatedTaskRequest request, CancellationToken ct = default);
    Task CancelAsync(Guid taskId, string managerUserId, CancellationToken ct = default, Guid? overrideGroupId = null);
}

public class ExpertDelegatedTaskService(
    IApplicationDbContext db,
    IExpertGroupManagerService managers) : IExpertDelegatedTaskService
{
    public Task<IReadOnlyList<ExpertDelegatedTaskDto>> ListForManagerAsync(
        string managerUserId, CancellationToken ct = default, Guid? overrideGroupId = null)
    {
        var groupId = overrideGroupId ?? RequireManagerGroupId(managerUserId);
        return Task.FromResult(MapList(db.ExpertDelegatedTasks
            .Where(t => t.ExpertGroupId == groupId)
            .OrderByDescending(t => t.CreatedAt)
            .ToList()));
    }

    public Task<IReadOnlyList<ExpertDelegatedTaskDto>> ListForAssigneeAsync(string expertUserId, CancellationToken ct = default)
    {
        return Task.FromResult(MapList(db.ExpertDelegatedTasks
            .Where(t => t.AssigneeExpertUserId == expertUserId
                        && t.Status != ExpertDelegatedTaskStatus.Cancelled)
            .OrderByDescending(t => t.CreatedAt)
            .ToList()));
    }

    public async Task<ExpertDelegatedTaskDto> CreateAsync(
        string managerUserId,
        CreateExpertDelegatedTaskRequest request,
        CancellationToken ct = default,
        Guid? overrideGroupId = null)
    {
        var groupId = overrideGroupId ?? RequireManagerGroupId(managerUserId);

        var assignee = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.ExpertGroupId == groupId
            && m.UserId == request.AssigneeExpertUserId
            && m.Status == ExpertMembershipStatus.Active)
            ?? throw new InvalidOperationException("L'Expert assigné doit être membre actif du groupe.");

        var teacher = db.Tenants.FirstOrDefault(t => t.Id == request.TeacherTenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");

        // Enseignant du périmètre groupe : approuvé par le groupe, ou en file du pays du groupe
        var approvedByGroup = teacher.ApprovedByExpertGroupId == groupId;
        var pendingForGroup = teacher.ExpertApprovalStatus is ExpertApprovalStatus.Pending
            or ExpertApprovalStatus.Assigned
            or ExpertApprovalStatus.UnderReview
            or ExpertApprovalStatus.ChangesRequested;
        if (!approvedByGroup && !pendingForGroup)
            throw new InvalidOperationException("Cet enseignant n'est pas dans le périmètre du groupe.");

        var task = new ExpertDelegatedTask
        {
            ExpertGroupId = groupId,
            TeacherTenantId = teacher.Id,
            TaskType = request.TaskType,
            Status = ExpertDelegatedTaskStatus.Open,
            CreatedByManagerUserId = managerUserId,
            AssigneeExpertUserId = assignee.UserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            DueAtUtc = request.DueAtUtc?.ToUniversalTime()
        };
        db.Add(task);
        await db.SaveChangesAsync(ct);
        return MapList([task]).First();
    }

    public async Task<ExpertDelegatedTaskDto> StartAsync(Guid taskId, string expertUserId, CancellationToken ct = default)
    {
        var task = RequireAssigneeTask(taskId, expertUserId);
        if (task.Status is not ExpertDelegatedTaskStatus.Open)
            throw new InvalidOperationException("Seule une tâche ouverte peut être démarrée.");
        task.Status = ExpertDelegatedTaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapList([task]).First();
    }

    public async Task<ExpertDelegatedTaskDto> CompleteAsync(
        Guid taskId, string expertUserId, CompleteExpertDelegatedTaskRequest request, CancellationToken ct = default)
    {
        var task = RequireAssigneeTask(taskId, expertUserId);
        if (task.Status is ExpertDelegatedTaskStatus.Cancelled or ExpertDelegatedTaskStatus.Completed)
            throw new InvalidOperationException("Cette tâche est déjà clôturée.");

        var teacher = db.Tenants.FirstOrDefault(t => t.Id == task.TeacherTenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");

        switch (task.TaskType)
        {
            case ExpertDelegatedTaskType.PublishTeacherProfile:
                // Délégué : la publication complète (licence / onboarding / public) est faite
                // via ITeacherSchoolAdminService côté API « publish-profile ».
                if (teacher.ExpertApprovalStatus != ExpertApprovalStatus.Approved)
                    throw new InvalidOperationException("L'enseignant doit être approuvé avant publication du profil.");
                teacher.Status = TenantStatus.Active;
                teacher.IsPublicProfile = true;
                teacher.OnboardingCompletedAt ??= DateTime.UtcNow;
                if (teacher.LicenseExpiresAt is null || teacher.LicenseExpiresAt <= DateTime.UtcNow)
                    teacher.LicenseExpiresAt = DateTime.UtcNow.AddYears(1);
                teacher.UpdatedAt = DateTime.UtcNow;
                break;
            case ExpertDelegatedTaskType.CreateTeacherProfile:
            case ExpertDelegatedTaskType.OrganizeTeacherAgenda:
                // Validation métier légère : notes de complétion requises pour tracer l'action.
                if (string.IsNullOrWhiteSpace(request.CompletionNotes))
                    throw new InvalidOperationException("Indiquez ce qui a été fait (notes de clôture).");
                break;
        }

        task.Status = ExpertDelegatedTaskStatus.Completed;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.CompletionNotes = string.IsNullOrWhiteSpace(request.CompletionNotes)
            ? null
            : request.CompletionNotes.Trim();
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapList([task]).First();
    }

    public async Task CancelAsync(
        Guid taskId, string managerUserId, CancellationToken ct = default, Guid? overrideGroupId = null)
    {
        var groupId = overrideGroupId ?? RequireManagerGroupId(managerUserId);
        var task = db.ExpertDelegatedTasks.FirstOrDefault(t => t.Id == taskId && t.ExpertGroupId == groupId)
            ?? throw new InvalidOperationException("Tâche introuvable.");
        task.Status = ExpertDelegatedTaskStatus.Cancelled;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private Guid RequireManagerGroupId(string managerUserId)
    {
        if (!managers.IsActiveManager(managerUserId))
            throw new InvalidOperationException("Accès réservé au Responsable actif.");

        var membership = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.UserId == managerUserId
            && m.Status == ExpertMembershipStatus.Active
            && m.MemberRole == ExpertGroupMemberRole.Manager)
            ?? throw new InvalidOperationException("Responsable sans adhésion active.");
        return membership.ExpertGroupId;
    }

    private ExpertDelegatedTask RequireAssigneeTask(Guid taskId, string expertUserId) =>
        db.ExpertDelegatedTasks.FirstOrDefault(t => t.Id == taskId && t.AssigneeExpertUserId == expertUserId)
        ?? throw new InvalidOperationException("Tâche introuvable ou non assignée à vous.");

    private IReadOnlyList<ExpertDelegatedTaskDto> MapList(List<ExpertDelegatedTask> tasks)
    {
        var tenantIds = tasks.Select(t => t.TeacherTenantId).Distinct().ToList();
        var tenants = db.Tenants.Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => t);

        return tasks.Select(t =>
        {
            tenants.TryGetValue(t.TeacherTenantId, out var teacher);
            return new ExpertDelegatedTaskDto(
                t.Id,
                t.ExpertGroupId,
                t.TeacherTenantId,
                teacher?.Name ?? "—",
                t.TaskType,
                t.Status,
                t.CreatedByManagerUserId,
                t.AssigneeExpertUserId,
                null,
                t.Notes,
                t.DueAtUtc,
                t.CreatedAt,
                t.CompletedAtUtc,
                t.CompletionNotes,
                teacher?.IsPublicProfile ?? false);
        }).ToList();
    }
}
