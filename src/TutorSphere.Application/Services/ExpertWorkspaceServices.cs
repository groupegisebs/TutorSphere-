using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.DTOs.Lessons;
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

    /// <summary>Journal paginé : le total permet d'afficher le nombre de pages réel.</summary>
    Task<ExpertGovernanceEventPageDto> ListPageForGroupAsync(
        string expertUserId, int page, int pageSize, bool notificationsOnly = false,
        int? eventType = null, string? search = null, CancellationToken ct = default);

    Task MarkReadAsync(Guid eventId, string expertUserId, CancellationToken ct = default);
    Task MarkAllNotificationsReadAsync(string expertUserId, CancellationToken ct = default);
}

public class ExpertGovernanceAuditService(IApplicationDbContext db, IUserContactLookup contacts)
    : IExpertGovernanceAuditService
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

    public async Task<IReadOnlyList<ExpertGovernanceEventDto>> ListForGroupAsync(
        string expertUserId, int take = 100, bool notificationsOnly = false, CancellationToken ct = default)
    {
        var list = Scope(expertUserId, notificationsOnly, null, null)
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToList();
        return await MapAsync(list, ct);
    }

    public async Task<ExpertGovernanceEventPageDto> ListPageForGroupAsync(
        string expertUserId, int page, int pageSize, bool notificationsOnly = false,
        int? eventType = null, string? search = null, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 5, 200);
        var q = Scope(expertUserId, notificationsOnly, eventType, search);
        var total = q.Count();
        // Une page demandée au-delà du total renverrait du vide : on retombe sur la dernière page.
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);

        var rows = q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return new ExpertGovernanceEventPageDto(await MapAsync(rows, ct), total, page, pageSize);
    }

    /// <summary>Événements des groupes où l'utilisateur est membre actif, filtres appliqués.</summary>
    private IQueryable<ExpertGovernanceEvent> Scope(
        string expertUserId, bool notificationsOnly, int? eventType, string? search)
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
        if (eventType is int t)
            q = q.Where(e => (int)e.EventType == t);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            q = q.Where(e => e.Summary.Contains(needle));
        }
        return q;
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

    /// <summary>Le journal affiche l'acteur : sans ce nom, la colonne montrait l'identifiant brut.</summary>
    private async Task<IReadOnlyList<ExpertGovernanceEventDto>> MapAsync(
        List<ExpertGovernanceEvent> list, CancellationToken ct)
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        var result = new List<ExpertGovernanceEventDto>(list.Count);
        foreach (var e in list)
        {
            if (!names.TryGetValue(e.ActorUserId, out var name))
            {
                name = (await contacts.GetAsync(e.ActorUserId, ct))?.DisplayName;
                names[e.ActorUserId] = name;
            }
            result.Add(new ExpertGovernanceEventDto(
                e.Id, e.ExpertGroupId, e.EventType, e.ActorUserId, name, e.Summary,
                e.RelatedTenantId, e.RelatedEntityId, e.IsNotification, e.ReadAtUtc, e.CreatedAt));
        }
        return result;
    }
}

public interface IExpertWorkspaceService
{
    Task<IReadOnlyList<ExpertWorkspaceItemDto>> ListAsync(
        string expertUserId, ExpertWorkspaceItemType type, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> CreateAsync(
        string expertUserId, CreateExpertWorkspaceItemRequest request, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> StartAsync(Guid id, string expertUserId, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> UpdatePayloadAsync(
        Guid id, string expertUserId, string? payloadJson, CancellationToken ct = default);
    Task<ExpertWorkspaceItemDto> CompleteAsync(
        Guid id, string expertUserId, CompleteExpertWorkspaceItemRequest request, CancellationToken ct = default);
    Task<LessonDto> GetDemonstrationClassroomAsync(Guid lessonId, string expertUserId, CancellationToken ct = default);
}

public class ExpertWorkspaceService(
    IApplicationDbContext db,
    IExpertGovernanceAuditService audit,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls,
    IRealTimeMessaging realtime) : IExpertWorkspaceService
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

        if (request.ItemType == ExpertWorkspaceItemType.Demonstration)
        {
            var preview = DemonstrationPayloadJson.Parse(request.PayloadJson);
            if (preview.EvaluatorUserIds.Count == 0)
                throw new InvalidOperationException(
                    "Invitez au moins un expert du groupe à participer à la démonstration.");
            foreach (var evaluatorId in preview.EvaluatorUserIds)
                EnsureMember(groupId, evaluatorId);
        }

        var item = new ExpertWorkspaceItem
        {
            ExpertGroupId = groupId,
            ItemType = request.ItemType,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            RelatedTeacherTenantId = request.RelatedTeacherTenantId,
            CreatedByUserId = expertUserId,
            AssignedToUserId = string.IsNullOrWhiteSpace(request.AssignedToUserId) ? null : request.AssignedToUserId.Trim(),
            ScheduledAtUtc = request.ScheduledAtUtc?.ToUniversalTime(),
            PayloadJson = NormalizePayload(groupId, request.ItemType, request.PayloadJson)
        };
        db.Add(item);
        await db.SaveChangesAsync(ct);

        if (item.ItemType == ExpertWorkspaceItemType.Demonstration)
            await ProvisionDemonstrationSessionAsync(item, expertUserId, ct);

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
        if (item.ItemType == ExpertWorkspaceItemType.Demonstration)
        {
            var payload = DemonstrationPayloadJson.Parse(item.PayloadJson);
            payload.SessionOpenedAtUtc ??= DateTime.UtcNow;
            if (payload.Step < 2)
                payload.Step = 2;
            item.PayloadJson = PersistPayload(payload);
        }
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await NotifyDemonstrationStartedAsync(item, ct);
        return Map([item]).First();
    }

    public async Task<ExpertWorkspaceItemDto> UpdatePayloadAsync(
        Guid id, string expertUserId, string? payloadJson, CancellationToken ct = default)
    {
        var item = RequireItem(id, expertUserId);
        if (item.Status == ExpertWorkspaceItemStatus.Cancelled)
            throw new InvalidOperationException("Élément annulé.");
        item.PayloadJson = NormalizePayload(item.ExpertGroupId, item.ItemType, payloadJson);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map([item]).First();
    }

    public async Task<LessonDto> GetDemonstrationClassroomAsync(Guid lessonId, string expertUserId, CancellationToken ct = default)
    {
        var groupId = RequireGroupId(expertUserId);
        var item = FindDemonstrationByLesson(groupId, lessonId)
            ?? throw new InvalidOperationException("Séance de démonstration introuvable.");

        var payload = DemonstrationPayloadJson.Parse(item.PayloadJson);
        var isEvaluator = payload.EvaluatorUserIds.Contains(expertUserId, StringComparer.Ordinal);
        var isCreator = string.Equals(item.CreatedByUserId, expertUserId, StringComparison.Ordinal);
        if (!isEvaluator && !isCreator)
            throw new InvalidOperationException("Vous n'êtes pas invité à cette démonstration.");

        if (isEvaluator)
        {
            var inv = payload.Invitations.FirstOrDefault(i =>
                string.Equals(i.UserId, expertUserId, StringComparison.Ordinal));
            if (inv is not null && inv.Status == 0)
            {
                inv.Status = 1;
                inv.RespondedAtUtc = DateTime.UtcNow;
                item.PayloadJson = PersistPayload(payload);
                item.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        var lesson = db.LessonsForAnyTenant.FirstOrDefault(l => l.Id == lessonId)
            ?? throw new InvalidOperationException("La salle de cours n'est plus disponible.");
        return MapLesson(lesson);
    }

    public async Task<ExpertWorkspaceItemDto> CompleteAsync(
        Guid id, string expertUserId, CompleteExpertWorkspaceItemRequest request, CancellationToken ct = default)
    {
        var item = RequireItem(id, expertUserId);
        if (item.Status == ExpertWorkspaceItemStatus.Cancelled)
            throw new InvalidOperationException("Élément annulé.");

        if (item.ItemType == ExpertWorkspaceItemType.Demonstration)
        {
            var payload = DemonstrationPayloadJson.Parse(item.PayloadJson);
            if (payload.Recommendation is 0)
                throw new InvalidOperationException(
                    "Choisissez une recommandation : Approuver, À améliorer, Nouvelle démonstration ou Refuser.");
            payload.Step = 5;
            if (string.IsNullOrWhiteSpace(payload.ReportText) && !string.IsNullOrWhiteSpace(request.OutcomeNotes))
                payload.ReportText = request.OutcomeNotes.Trim();
            item.PayloadJson = PersistPayload(payload);
            if (string.IsNullOrWhiteSpace(request.OutcomeNotes) && !string.IsNullOrWhiteSpace(payload.ReportText))
                request = request with { OutcomeNotes = payload.ReportText };
        }

        item.Status = ExpertWorkspaceItemStatus.Done;
        item.CompletedAtUtc = DateTime.UtcNow;
        item.OutcomeNotes = string.IsNullOrWhiteSpace(request.OutcomeNotes)
            ? item.OutcomeNotes
            : Truncate(request.OutcomeNotes.Trim(), 2000);
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
            i.ScheduledAtUtc, i.CompletedAtUtc, i.OutcomeNotes, i.CreatedAt, i.PayloadJson)).ToList();
    }

    private string? NormalizePayload(Guid groupId, ExpertWorkspaceItemType type, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        if (type != ExpertWorkspaceItemType.Demonstration)
            return Truncate(json.Trim(), 8000);

        var payload = DemonstrationPayloadJson.Parse(json);
        if (payload.DurationMinutes is < 15 or > 180)
            payload.DurationMinutes = 45;
        payload.EvaluatorUserIds = payload.EvaluatorUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var evaluatorId in payload.EvaluatorUserIds)
            EnsureMember(groupId, evaluatorId);
        if (payload.Step is < 1 or > 5)
            payload.Step = 1;
        return PersistPayload(payload);
    }

    private static string PersistPayload(DemonstrationPayload payload)
    {
        var json = DemonstrationPayloadJson.Serialize(payload);
        if (json.Length > 32000)
            throw new InvalidOperationException("Le compte rendu ou la grille dépasse la taille autorisée.");
        return json;
    }

    private async Task ProvisionDemonstrationSessionAsync(
        ExpertWorkspaceItem item, string actorUserId, CancellationToken ct)
    {
        var payload = DemonstrationPayloadJson.Parse(item.PayloadJson);
        if (payload.EvaluatorUserIds.Count == 0)
            throw new InvalidOperationException("Invitez au moins un expert du groupe à participer à la démonstration.");

        item.ScheduledAtUtc ??= DateTime.UtcNow.AddHours(1);
        var start = item.ScheduledAtUtc.Value;
        var end = start.AddMinutes(payload.DurationMinutes > 0 ? payload.DurationMinutes : 45);
        var teacher = item.RelatedTeacherTenantId is Guid tid
            ? db.Tenants.FirstOrDefault(t => t.Id == tid)
            : null;
        var teacherName = teacher?.Name ?? "Enseignant";
        var subject = string.IsNullOrWhiteSpace(payload.Subject) ? item.Title : payload.Subject.Trim();

        if (teacher is not null && payload.LessonId is null)
        {
            var lesson = new Lesson
            {
                TenantId = teacher.Id,
                Title = item.Title,
                Description = string.IsNullOrWhiteSpace(payload.Topic)
                    ? "Démonstration pédagogique — évaluation par le groupe."
                    : payload.Topic.Trim(),
                Subject = subject,
                StartTime = start,
                EndTime = end,
                Mode = IsDistant(payload.Location) ? LessonMode.Online
                    : string.Equals(payload.Location, "Hybride", StringComparison.OrdinalIgnoreCase)
                      || (payload.Location?.StartsWith("Hybride", StringComparison.OrdinalIgnoreCase) ?? false)
                        ? LessonMode.Hybrid
                        : LessonMode.InPerson,
                Location = payload.Location,
                SessionNotes = "demonstration",
                MaxStudents = Math.Max(payload.EvaluatorUserIds.Count, 1)
            };
            db.Add(lesson);
            await db.SaveChangesAsync(ct);
            payload.LessonId = lesson.Id;
            var web = urls.WebBaseUrl.TrimEnd('/');
            lesson.MeetingUrl = $"{web}/expert/classroom/{lesson.Id}";
            await db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        payload.Invitations = [];
        foreach (var userId in payload.EvaluatorUserIds)
        {
            var contact = await contacts.GetAsync(userId, ct);
            payload.Invitations.Add(new DemonstrationInvitation
            {
                UserId = userId,
                Name = contact?.DisplayName,
                Email = contact?.Email,
                Status = 0,
                InvitedAtUtc = now
            });

            if (contact is null) continue;
            try
            {
                await email.SendLessonScheduledAsync(
                    contact.Value.Email,
                    contact.Value.DisplayName,
                    teacherName,
                    subject,
                    start,
                    ct);
            }
            catch
            {
                // L'invitation in-app reste disponible même si l'e-mail échoue.
            }
        }

        if (!string.IsNullOrWhiteSpace(teacher?.OwnerUserId))
        {
            var tutorContact = await contacts.GetAsync(teacher.OwnerUserId, ct);
            if (tutorContact is not null)
            {
                try
                {
                    await email.SendLessonScheduledAsync(
                        tutorContact.Value.Email,
                        tutorContact.Value.DisplayName,
                        teacherName,
                        subject,
                        start,
                        ct);
                }
                catch { /* invitation calendrier déjà créée */ }
            }
        }

        item.PayloadJson = PersistPayload(payload);
        await db.SaveChangesAsync(ct);
        _ = actorUserId;
    }

    private async Task NotifyDemonstrationStartedAsync(ExpertWorkspaceItem item, CancellationToken ct)
    {
        if (item.ItemType != ExpertWorkspaceItemType.Demonstration)
            return;

        var payload = DemonstrationPayloadJson.Parse(item.PayloadJson);
        if (payload.LessonId is not Guid lessonId)
            return;

        var recipients = new HashSet<string>(payload.EvaluatorUserIds, StringComparer.Ordinal);
        if (item.RelatedTeacherTenantId is Guid tid)
        {
            var owner = db.Tenants.Where(t => t.Id == tid).Select(t => t.OwnerUserId).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(owner))
                recipients.Add(owner);
        }

        if (recipients.Count == 0) return;
        var teacherName = item.RelatedTeacherTenantId is Guid t2
            ? db.Tenants.Where(t => t.Id == t2).Select(t => t.Name).FirstOrDefault() ?? "Enseignant"
            : "Enseignant";
        await realtime.NotifyLessonStartedAsync(
            recipients,
            new LessonStartedNotificationDto(
                lessonId,
                item.Title,
                payload.Subject,
                teacherName,
                DateTime.UtcNow),
            ct);
    }

    private ExpertWorkspaceItem? FindDemonstrationByLesson(Guid groupId, Guid lessonId)
    {
        var token = lessonId.ToString();
        return db.ExpertWorkspaceItems
            .Where(i => i.ExpertGroupId == groupId && i.ItemType == ExpertWorkspaceItemType.Demonstration)
            .AsEnumerable()
            .FirstOrDefault(i =>
                i.PayloadJson is not null
                && i.PayloadJson.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static LessonDto MapLesson(Lesson lesson) =>
        new(
            lesson.Id,
            lesson.Title,
            lesson.Description,
            lesson.Subject,
            lesson.StartTime,
            lesson.EndTime,
            lesson.Mode.ToString(),
            lesson.Location,
            lesson.MeetingUrl,
            lesson.SessionNotes,
            lesson.CreatedAt,
            lesson.UpdatedAt,
            lesson.SettlementStatus.ToString(),
            lesson.CancelledAt,
            lesson.SessionCounted,
            lesson.TutorLiable,
            lesson.TutorLiabilityResolution,
            lesson.MaxStudents);

    private static bool IsDistant(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return true;
        var loc = location.Trim();
        return loc.Equals("Distant", StringComparison.OrdinalIgnoreCase)
               || loc.Equals("Distance", StringComparison.OrdinalIgnoreCase)
               || loc.Equals("En ligne", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
