using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertGovernanceAuditService
{
    Task RecordAsync(
        ExpertGovernanceEventType type,
        string actorUserId,
        string summary,
        Guid? expertGroupId = null,
        Guid? relatedTenantId = null,
        Guid? relatedEntityId = null,
        string? payloadJson = null,
        bool isNotification = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExpertGovernanceEventDto>> ListForGroupAsync(
        string expertUserId, int take = 100, bool notificationsOnly = false, CancellationToken ct = default);

    Task MarkReadAsync(Guid eventId, string expertUserId, CancellationToken ct = default);
    Task MarkAllNotificationsReadAsync(string expertUserId, CancellationToken ct = default);
}

public class ExpertGovernanceAuditService(IApplicationDbContext db) : IExpertGovernanceAuditService
{
    public async Task RecordAsync(
        ExpertGovernanceEventType type,
        string actorUserId,
        string summary,
        Guid? expertGroupId = null,
        Guid? relatedTenantId = null,
        Guid? relatedEntityId = null,
        string? payloadJson = null,
        bool isNotification = true,
        CancellationToken ct = default)
    {
        db.Add(new ExpertGovernanceEvent
        {
            ExpertGroupId = expertGroupId,
            EventType = type,
            ActorUserId = actorUserId,
            Summary = summary.Trim(),
            RelatedTenantId = relatedTenantId,
            RelatedEntityId = relatedEntityId,
            PayloadJson = payloadJson,
            IsNotification = isNotification
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ExpertGovernanceEventDto>> ListForGroupAsync(
        string expertUserId, int take = 100, bool notificationsOnly = false, CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToList();

        var q = db.ExpertGovernanceEvents
            .Where(e => e.ExpertGroupId.HasValue && groupIds.Contains(e.ExpertGroupId.Value));
        if (notificationsOnly)
            q = q.Where(e => e.IsNotification);

        var list = q.OrderByDescending(e => e.CreatedAt).Take(Math.Clamp(take, 1, 500)).ToList();
        return Task.FromResult(Map(list));
    }

    public async Task MarkReadAsync(Guid eventId, string expertUserId, CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();
        var ev = db.ExpertGovernanceEvents.FirstOrDefault(e =>
            e.Id == eventId && e.ExpertGroupId.HasValue && groupIds.Contains(e.ExpertGroupId.Value))
            ?? throw new InvalidOperationException("Événement introuvable.");
        ev.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllNotificationsReadAsync(string expertUserId, CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();
        var items = db.ExpertGovernanceEvents
            .Where(e => e.IsNotification && e.ReadAtUtc == null
                        && e.ExpertGroupId.HasValue && groupIds.Contains(e.ExpertGroupId.Value))
            .ToList();
        foreach (var e in items)
            e.ReadAtUtc = DateTime.UtcNow;
        if (items.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<ExpertGovernanceEventDto> Map(List<ExpertGovernanceEvent> list) =>
        list.Select(e => new ExpertGovernanceEventDto(
            e.Id, e.ExpertGroupId, e.EventType, e.ActorUserId, null, e.Summary,
            e.RelatedTenantId, e.RelatedEntityId, e.IsNotification, e.ReadAtUtc, e.CreatedAt)).ToList();
}

public interface IExpertWorkspaceService
{
    Task<IReadOnlyList<ExpertWorkspaceItemDto>> ListAsync(
        string expertUserId, ExpertWorkspaceItemType type, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> CreateAsync(
        string expertUserId, CreateExpertWorkspaceItemRequest request, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> StartAsync(Guid id, string expertUserId, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> CompleteAsync(
        Guid id, string expertUserId, CompleteExpertWorkspaceItemRequest request, CancellationToken ct = default);
}

public class ExpertWorkspaceService(
    IApplicationDbContext db,
    IExpertGovernanceAuditService audit) : IExpertWorkspaceService
{
    public Task<IReadOnlyList<ExpertWorkspaceItemDto>> ListAsync(
        string expertUserId, ExpertWorkspaceItemType type, CancellationToken ct = default)
    {
        var groupId = RequireGroupId(expertUserId);
        var items = db.ExpertWorkspaceItems
            .Where(i => i.ExpertGroupId == groupId && i.ItemType == type)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
        return Task.FromResult(Map(items));
    }

    public async Task<ExpertWorkspaceItemDto> CreateAsync(
        string expertUserId, CreateExpertWorkspaceItemRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Le titre est obligatoire.");

        var groupId = RequireGroupId(expertUserId);
        if (request.RelatedTeacherTenantId is Guid tid)
        {
            _ = db.Tenants.FirstOrDefault(t => t.Id == tid)
                ?? throw new InvalidOperationException("Enseignant introuvable.");
        }

        if (!string.IsNullOrWhiteSpace(request.AssignedToUserId))
            EnsureMember(groupId, request.AssignedToUserId);

        var item = new ExpertWorkspaceItem
        {
            ExpertGroupId = groupId,
            ItemType = request.ItemType,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            RelatedTeacherTenantId = request.RelatedTeacherTenantId,
            CreatedByUserId = expertUserId,
            AssignedToUserId = string.IsNullOrWhiteSpace(request.AssignedToUserId) ? null : request.AssignedToUserId.Trim(),
            ScheduledAtUtc = request.ScheduledAtUtc?.ToUniversalTime()
        };
        db.Add(item);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.WorkspaceItemCreated,
            expertUserId,
            $"Création « {item.Title} » ({item.ItemType})",
            groupId,
            item.RelatedTeacherTenantId,
            item.Id,
            ct: ct);

        return Map([item]).First();
    }

    public async Task<ExpertWorkspaceItemDto> StartAsync(Guid id, string expertUserId, CancellationToken ct = default)
    {
        var item = RequireItem(id, expertUserId);
        if (item.Status is ExpertWorkspaceItemStatus.Done or ExpertWorkspaceItemStatus.Cancelled)
            throw new InvalidOperationException("Élément déjà clôturé.");
        item.Status = ExpertWorkspaceItemStatus.InProgress;
        item.AssignedToUserId ??= expertUserId;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map([item]).First();
    }

    public async Task<ExpertWorkspaceItemDto> CompleteAsync(
        Guid id, string expertUserId, CompleteExpertWorkspaceItemRequest request, CancellationToken ct = default)
    {
        var item = RequireItem(id, expertUserId);
        if (item.Status == ExpertWorkspaceItemStatus.Cancelled)
            throw new InvalidOperationException("Élément annulé.");
        item.Status = ExpertWorkspaceItemStatus.Done;
        item.CompletedAtUtc = DateTime.UtcNow;
        item.OutcomeNotes = string.IsNullOrWhiteSpace(request.OutcomeNotes) ? null : request.OutcomeNotes.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.WorkspaceItemCompleted,
            expertUserId,
            $"Clôture « {item.Title} »",
            item.ExpertGroupId,
            item.RelatedTeacherTenantId,
            item.Id,
            ct: ct);

        return Map([item]).First();
    }

    private Guid RequireGroupId(string userId)
    {
        var groupId = db.ExpertGroupMembers
            .Where(m => m.UserId == userId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
        if (groupId == Guid.Empty)
            throw new InvalidOperationException("Aucun groupe Expert actif.");
        return groupId;
    }

    private void EnsureMember(Guid groupId, string userId)
    {
        var ok = db.ExpertGroupMembers.Any(m =>
            m.ExpertGroupId == groupId && m.UserId == userId && m.Status == ExpertMembershipStatus.Active);
        if (!ok) throw new InvalidOperationException("L'assigné doit être membre actif du groupe.");
    }

    private ExpertWorkspaceItem RequireItem(Guid id, string userId)
    {
        var groupId = RequireGroupId(userId);
        return db.ExpertWorkspaceItems.FirstOrDefault(i => i.Id == id && i.ExpertGroupId == groupId)
            ?? throw new InvalidOperationException("Élément introuvable.");
    }

    private IReadOnlyList<ExpertWorkspaceItemDto> Map(List<ExpertWorkspaceItem> items)
    {
        var tenantIds = items.Where(i => i.RelatedTeacherTenantId.HasValue)
            .Select(i => i.RelatedTeacherTenantId!.Value).Distinct().ToList();
        var tenants = db.Tenants.Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => t.Name);

        return items.Select(i => new ExpertWorkspaceItemDto(
            i.Id, i.ExpertGroupId, i.ItemType, i.Status, i.Title, i.Description,
            i.RelatedTeacherTenantId,
            i.RelatedTeacherTenantId is Guid tid && tenants.TryGetValue(tid, out var n) ? n : null,
            i.CreatedByUserId, i.AssignedToUserId, null,
            i.ScheduledAtUtc, i.CompletedAtUtc, i.OutcomeNotes, i.CreatedAt)).ToList();
    }
}
