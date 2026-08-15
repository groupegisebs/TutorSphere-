using System.Globalization;

namespace TutorSphere.Application.Common;

/// <summary>
/// Plages de disponibilité vs créneaux de réservation (durée de séance indépendante).
/// </summary>
public static class AvailabilityWindows
{
    public static bool TryParseTime(string? value, out TimeSpan span)
    {
        span = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var t = value.Trim();
        if (TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out span))
            return true;
        if (TimeOnly.TryParse(t, CultureInfo.InvariantCulture, out var only))
        {
            span = only.ToTimeSpan();
            return true;
        }

        return false;
    }

    public static bool TryParseDay(string? day, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;
        if (string.IsNullOrWhiteSpace(day))
            return false;

        var key = day.Trim().ToLowerInvariant();
        dayOfWeek = key switch
        {
            "lundi" or "lun" or "monday" or "mon" or "1" => DayOfWeek.Monday,
            "mardi" or "mar" or "tuesday" or "tue" or "2" => DayOfWeek.Tuesday,
            "mercredi" or "mer" or "wednesday" or "wed" or "3" => DayOfWeek.Wednesday,
            "jeudi" or "jeu" or "thursday" or "thu" or "4" => DayOfWeek.Thursday,
            "vendredi" or "ven" or "friday" or "fri" or "5" => DayOfWeek.Friday,
            "samedi" or "sam" or "saturday" or "sat" or "6" => DayOfWeek.Saturday,
            "dimanche" or "dim" or "sunday" or "sun" or "0" or "7" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1)
        };
        return (int)dayOfWeek >= 0;
    }

    public static string DayLabelFr(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Lundi",
        DayOfWeek.Tuesday => "Mardi",
        DayOfWeek.Wednesday => "Mercredi",
        DayOfWeek.Thursday => "Jeudi",
        DayOfWeek.Friday => "Vendredi",
        DayOfWeek.Saturday => "Samedi",
        DayOfWeek.Sunday => "Dimanche",
        _ => d.ToString()
    };

    /// <summary>Découpe une plage en créneaux de <paramref name="sessionDurationMin"/> minutes (reste trop court ignoré).</summary>
    public static IReadOnlyList<(TimeSpan Start, TimeSpan End)> ToBookingSlots(
        TimeSpan rangeStart,
        TimeSpan rangeEnd,
        int sessionDurationMin)
    {
        var list = new List<(TimeSpan, TimeSpan)>();
        if (rangeEnd <= rangeStart)
            return list;

        var minutes = sessionDurationMin > 0 ? sessionDurationMin : 60;
        var duration = TimeSpan.FromMinutes(minutes);
        for (var t = rangeStart; t + duration <= rangeEnd; t += duration)
            list.Add((t, t + duration));
        return list;
    }

    public static int CountBookingSlots(TimeSpan rangeStart, TimeSpan rangeEnd, int sessionDurationMin) =>
        ToBookingSlots(rangeStart, rangeEnd, sessionDurationMin).Count;

    public static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd) =>
        aStart < bEnd && bStart < aEnd;
}
