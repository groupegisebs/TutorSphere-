using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>
/// Empêche un élève d'être inscrit à deux cours / offres dont les horaires se chevauchent
/// (il ne peut pas être dans deux salles au même moment).
/// </summary>
public static class StudentScheduleConflictChecker
{
    public static bool RangesOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;

    /// <summary>
    /// Vérifie qu'un nouvel intervalle de séance ne chevauche pas les cours déjà planifiés pour l'élève.
    /// </summary>
    public static void EnsureNoLessonConflict(
        IApplicationDbContext db,
        Guid studentId,
        DateTime start,
        DateTime end,
        Guid? excludeLessonId = null)
    {
        var startUtc = ToUtc(start);
        var endUtc = ToUtc(end);
        if (endUtc <= startUtc)
            return;

        var lessonIds = db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == studentId)
            .Select(a => a.LessonId)
            .Distinct()
            .ToList();

        if (lessonIds.Count == 0)
            return;

        var conflicting = db.LessonsForAnyTenant
            .Where(l => lessonIds.Contains(l.Id)
                        && l.SettlementStatus != LessonSettlementStatus.CancelledFree
                        && (excludeLessonId == null || l.Id != excludeLessonId.Value)
                        && l.StartTime < endUtc
                        && startUtc < l.EndTime)
            .OrderBy(l => l.StartTime)
            .Select(l => new { l.Title, l.StartTime, l.EndTime })
            .FirstOrDefault();

        if (conflicting is null)
            return;

        throw new InvalidOperationException(
            FormatLessonConflict(conflicting.Title, conflicting.StartTime, conflicting.EndTime));
    }

    /// <summary>
    /// Vérifie qu'une nouvelle offre ne chevauche pas les abonnements actifs/en attente
    /// ni les séances déjà planifiées de l'élève.
    /// </summary>
    public static void EnsureNoOfferingConflict(
        IApplicationDbContext db,
        Guid studentId,
        Guid newOfferingId,
        string? newOfferingConditions,
        DateTime subscriptionStart,
        DateTime subscriptionEnd)
    {
        var newSchedule = OfferScheduleCalendarExpander.TryParseSchedule(newOfferingConditions);
        if (newSchedule?.Slots is { Count: > 0 })
        {
            var existingSubs = db.StudentSubscriptionsForAnyTenant
                .Where(s => s.StudentId == studentId
                            && s.OfferingId != newOfferingId
                            && (s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.Active))
                .ToList();

            if (existingSubs.Count > 0)
            {
                var offeringIds = existingSubs.Select(s => s.OfferingId).Distinct().ToList();
                var offerings = db.SubscriptionOfferingsForAnyTenant
                    .Where(o => offeringIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                foreach (var sub in existingSubs)
                {
                    if (!offerings.TryGetValue(sub.OfferingId, out var other))
                        continue;

                    var otherSchedule = OfferScheduleCalendarExpander.TryParseSchedule(other.Conditions);
                    if (otherSchedule?.Slots is null || otherSchedule.Slots.Count == 0)
                        continue;

                    if (WeeklySchedulesOverlap(newSchedule, otherSchedule, out var dayLabel, out var timeLabel))
                    {
                        throw new InvalidOperationException(
                            $"Impossible de s'inscrire : le créneau « {dayLabel} {timeLabel} » chevauche l'offre « {other.Title} ». " +
                            "Choisissez des heures différentes — un élève ne peut pas être dans deux salles au même moment.");
                    }
                }
            }

            // Vérifie aussi contre les séances concrètes déjà au calendrier.
            var from = subscriptionStart.Date;
            var to = subscriptionEnd.Date.AddDays(1);
            if (to <= from)
                to = from.AddDays(90);

            var anchor = DateTime.Today;
            var occurrences = OfferScheduleCalendarExpander
                .ExpandOccurrences(newSchedule, from, to, anchor)
                .Take(52)
                .ToList();

            foreach (var (start, end) in occurrences)
            {
                EnsureNoLessonConflict(
                    db,
                    studentId,
                    DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime(),
                    DateTime.SpecifyKind(end, DateTimeKind.Local).ToUniversalTime());
            }
        }
    }

    public static bool WeeklySchedulesOverlap(
        OfferingScheduleDto a,
        OfferingScheduleDto b,
        out string dayLabel,
        out string timeLabel)
    {
        dayLabel = "";
        timeLabel = "";
        var durA = a.SessionDurationMin > 0 ? a.SessionDurationMin : 60;
        var durB = b.SessionDurationMin > 0 ? b.SessionDurationMin : 60;

        foreach (var slotA in a.Slots ?? [])
        {
            if (!TryParseDay(slotA.Day, out var dayA) || !TryParseTime(slotA.Time, out var timeA))
                continue;
            var endA = timeA.Add(TimeSpan.FromMinutes(durA));

            foreach (var slotB in b.Slots ?? [])
            {
                if (!TryParseDay(slotB.Day, out var dayB) || !TryParseTime(slotB.Time, out var timeB))
                    continue;
                if (dayA != dayB)
                    continue;

                var endB = timeB.Add(TimeSpan.FromMinutes(durB));
                if (timeA < endB && timeB < endA)
                {
                    dayLabel = DayLabelFr(dayA);
                    timeLabel = $"{FormatTime(timeA)}–{FormatTime(endA)}";
                    return true;
                }
            }
        }

        return false;
    }

    private static string FormatLessonConflict(string title, DateTime start, DateTime end)
    {
        var localStart = start.Kind == DateTimeKind.Utc ? start.ToLocalTime() : start;
        var localEnd = end.Kind == DateTimeKind.Utc ? end.ToLocalTime() : end;
        return
            $"Impossible : ce créneau chevauche le cours « {title} » " +
            $"({localStart:dddd dd/MM HH:mm}–{localEnd:HH:mm}). " +
            "Choisissez des heures différentes — un élève ne peut pas être dans deux salles au même moment.";
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static bool TryParseDay(string? day, out DayOfWeek dow)
    {
        dow = default;
        if (string.IsNullOrWhiteSpace(day)) return false;
        var key = day.Trim().ToLowerInvariant();
        dow = key switch
        {
            "lundi" or "lun" or "monday" or "mon" => DayOfWeek.Monday,
            "mardi" or "mar" or "tuesday" or "tue" => DayOfWeek.Tuesday,
            "mercredi" or "mer" or "wednesday" or "wed" => DayOfWeek.Wednesday,
            "jeudi" or "jeu" or "thursday" or "thu" => DayOfWeek.Thursday,
            "vendredi" or "ven" or "friday" or "fri" => DayOfWeek.Friday,
            "samedi" or "sam" or "saturday" or "sat" => DayOfWeek.Saturday,
            "dimanche" or "dim" or "sunday" or "sun" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1)
        };
        return (int)dow >= 0;
    }

    private static bool TryParseTime(string? time, out TimeSpan span)
    {
        span = default;
        if (string.IsNullOrWhiteSpace(time)) return false;
        if (TimeSpan.TryParse(time.Trim(), out span)) return true;
        if (TimeOnly.TryParse(time.Trim(), out var t))
        {
            span = t.ToTimeSpan();
            return true;
        }
        return false;
    }

    private static string DayLabelFr(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "lundi",
        DayOfWeek.Tuesday => "mardi",
        DayOfWeek.Wednesday => "mercredi",
        DayOfWeek.Thursday => "jeudi",
        DayOfWeek.Friday => "vendredi",
        DayOfWeek.Saturday => "samedi",
        DayOfWeek.Sunday => "dimanche",
        _ => d.ToString()
    };

    private static string FormatTime(TimeSpan t) =>
        $"{(int)t.TotalHours:D2}:{t.Minutes:D2}";
}
