using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ISubscriptionLessonScheduler
{
    /// <summary>
    /// Crée les séances (Lesson + attendance) à partir du planning de l'offre
    /// lorsque l'abonnement devient Active. Idempotent.
    /// </summary>
    Task<int> EnsureScheduledAsync(Guid subscriptionId, CancellationToken ct = default);
}

public sealed class SubscriptionLessonScheduler : ISubscriptionLessonScheduler
{
    public const string SessionNotesMarkerPrefix = "#sub:";

    private readonly IApplicationDbContext _db;
    private readonly ILogger<SubscriptionLessonScheduler> _logger;

    public SubscriptionLessonScheduler(IApplicationDbContext db, ILogger<SubscriptionLessonScheduler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static string MarkerFor(Guid subscriptionId) =>
        $"{SessionNotesMarkerPrefix}{subscriptionId:N}";

    public async Task<int> EnsureScheduledAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = _db.StudentSubscriptionsForAnyTenant
            .FirstOrDefault(s => s.Id == subscriptionId);
        if (subscription is null)
            return 0;

        if (subscription.Status != SubscriptionStatus.Active)
            return 0;

        var marker = MarkerFor(subscription.Id);
        var alreadyScheduled = _db.LessonsForAnyTenant
            .Any(l => l.SessionNotes != null && l.SessionNotes.Contains(marker));
        if (alreadyScheduled)
            return 0;

        var offering = _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefault(o => o.Id == subscription.OfferingId);
        if (offering is null)
        {
            _logger.LogWarning(
                "Impossible de planifier les cours : offre {OfferingId} introuvable pour abonnement {SubscriptionId}",
                subscription.OfferingId,
                subscriptionId);
            return 0;
        }

        var schedule = OfferScheduleCalendarExpander.TryParseSchedule(offering.Conditions);
        if (schedule?.Slots is null || schedule.Slots.Count == 0)
        {
            _logger.LogWarning(
                "Offre {OfferingId} sans créneaux — aucun cours créé pour abonnement {SubscriptionId}",
                offering.Id,
                subscriptionId);
            return 0;
        }

        var sessionTarget = Math.Max(1, subscription.SessionsRemaining > 0
            ? subscription.SessionsRemaining
            : offering.SessionCount);

        var fromLocal = DateTime.Today;
        var subStartLocal = subscription.StartDate.Kind == DateTimeKind.Utc
            ? subscription.StartDate.ToLocalTime().Date
            : subscription.StartDate.Date;
        if (subStartLocal > fromLocal)
            fromLocal = subStartLocal;

        var subEndLocal = subscription.EndDate.Kind == DateTimeKind.Utc
            ? subscription.EndDate.ToLocalTime().Date
            : subscription.EndDate.Date;
        // Buffer: generate at least enough weeks for sessionTarget
        var weeksNeeded = Math.Max(8, (int)Math.Ceiling(sessionTarget / (double)Math.Max(1, schedule.Slots.Count)) + 2);
        var toLocal = subEndLocal.AddDays(1);
        var minEnd = fromLocal.AddDays(weeksNeeded * 7);
        if (toLocal < minEnd)
            toLocal = minEnd;

        var cadenceAnchor = offering.CreatedAt == default ? fromLocal : offering.CreatedAt.Date;
        var occurrences = OfferScheduleCalendarExpander
            .ExpandOccurrences(schedule, fromLocal, toLocal, cadenceAnchor)
            .Take(sessionTarget)
            .ToList();

        if (occurrences.Count == 0)
        {
            _logger.LogWarning(
                "Aucun créneau généré pour abonnement {SubscriptionId} (offre {OfferingId})",
                subscriptionId,
                offering.Id);
            return 0;
        }

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == subscription.StudentId);
        if (student is null)
            return 0;

        var created = new List<Lesson>();
        foreach (var (startLocal, endLocal) in occurrences)
        {
            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            var lesson = new Lesson
            {
                TenantId = subscription.TenantId,
                Title = offering.Title,
                Description = offering.Description,
                Subject = offering.Subject,
                StartTime = startUtc,
                EndTime = endUtc,
                Mode = offering.Mode,
                SessionNotes = marker
            };
            _db.Add(lesson);
            created.Add(lesson);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var lesson in created)
        {
            _db.Add(new LessonAttendance
            {
                TenantId = subscription.TenantId,
                LessonId = lesson.Id,
                StudentId = student.Id,
                IsPresent = false
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Planifié {Count} cours pour abonnement {SubscriptionId} (élève {StudentId}, offre {OfferingId})",
            created.Count,
            subscriptionId,
            student.Id,
            offering.Id);

        return created.Count;
    }
}
