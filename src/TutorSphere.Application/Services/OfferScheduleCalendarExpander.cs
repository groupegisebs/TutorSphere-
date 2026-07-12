using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TutorSphere.Application.DTOs.Calendar;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>
/// Expands offer schedule templates (JSON in Conditions) into concrete calendar windows
/// for a given date range — without creating Lesson rows.
/// </summary>
public static class OfferScheduleCalendarExpander
{
    private static readonly JsonSerializerOptions ScheduleJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<OfferAvailabilityDto> Expand(
        IEnumerable<SubscriptionOffering> offerings,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var results = new List<OfferAvailabilityDto>();
        var startDate = rangeStart.Date;

        foreach (var offering in offerings)
        {
            if (!offering.IsActive)
                continue;

            var schedule = TryParseSchedule(offering.Conditions);
            if (schedule?.Slots is null || schedule.Slots.Count == 0)
                continue;

            var durationMin = schedule.SessionDurationMin > 0 ? schedule.SessionDurationMin : 60;
            var cadence = (schedule.Cadence ?? "weekly").Trim().ToLowerInvariant();
            var mode = FormatMode(offering.Mode);
            var cadenceAnchor = offering.CreatedAt == default
                ? startDate
                : offering.CreatedAt.Date;

            foreach (var slot in schedule.Slots)
            {
                if (!TryParseDayOfWeek(slot.Day, out var dayOfWeek))
                    continue;
                if (!TryParseTime(slot.Time, out var time))
                    continue;

                for (var day = startDate; day < rangeEnd; day = day.AddDays(1))
                {
                    if (day.DayOfWeek != dayOfWeek)
                        continue;
                    if (!MatchesCadence(day, cadence, cadenceAnchor))
                        continue;

                    var start = DateTime.SpecifyKind(day.Add(time), DateTimeKind.Unspecified);
                    var end = start.AddMinutes(durationMin);
                    if (end <= rangeStart || start >= rangeEnd)
                        continue;

                    results.Add(new OfferAvailabilityDto(
                        offering.Id,
                        offering.Title,
                        offering.Subject,
                        start,
                        end,
                        mode,
                        cadence));
                }
            }
        }

        return results.OrderBy(r => r.StartTime).ToList();
    }

    private static bool MatchesCadence(DateTime day, string cadence, DateTime anchor)
    {
        if (cadence is not ("biweekly" or "fortnightly"))
            return true;

        var anchorMonday = StartOfWeekMonday(anchor);
        var dayMonday = StartOfWeekMonday(day);
        var weeks = (int)((dayMonday - anchorMonday).TotalDays / 7);
        return weeks % 2 == 0;
    }

    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    private static bool TryParseDayOfWeek(string? day, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;
        if (string.IsNullOrWhiteSpace(day))
            return false;

        var key = day.Trim().ToLowerInvariant();
        dayOfWeek = key switch
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
        return (int)dayOfWeek >= 0;
    }

    private static bool TryParseTime(string? time, out TimeSpan span)
    {
        span = default;
        if (string.IsNullOrWhiteSpace(time))
            return false;

        if (TimeSpan.TryParse(time.Trim(), CultureInfo.InvariantCulture, out span))
            return true;
        if (TimeOnly.TryParse(time.Trim(), CultureInfo.InvariantCulture, out var t))
        {
            span = t.ToTimeSpan();
            return true;
        }

        return false;
    }

    public static OfferingScheduleDto? TryParseSchedule(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(conditions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!doc.RootElement.TryGetProperty("slots", out _))
                return null;

            return JsonSerializer.Deserialize<OfferingScheduleDto>(conditions, ScheduleJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Occurrences locales (Kind=Unspecified) pour une plage donnée.</summary>
    public static IReadOnlyList<(DateTime Start, DateTime End)> ExpandOccurrences(
        OfferingScheduleDto schedule,
        DateTime rangeStart,
        DateTime rangeEnd,
        DateTime cadenceAnchor)
    {
        var results = new List<(DateTime Start, DateTime End)>();
        if (schedule.Slots is null || schedule.Slots.Count == 0)
            return results;

        var durationMin = schedule.SessionDurationMin > 0 ? schedule.SessionDurationMin : 60;
        var cadence = (schedule.Cadence ?? "weekly").Trim().ToLowerInvariant();
        var startDate = rangeStart.Date;

        foreach (var slot in schedule.Slots)
        {
            if (!TryParseDayOfWeek(slot.Day, out var dayOfWeek))
                continue;
            if (!TryParseTime(slot.Time, out var time))
                continue;

            for (var day = startDate; day < rangeEnd; day = day.AddDays(1))
            {
                if (day.DayOfWeek != dayOfWeek)
                    continue;
                if (!MatchesCadence(day, cadence, cadenceAnchor))
                    continue;

                var start = DateTime.SpecifyKind(day.Add(time), DateTimeKind.Unspecified);
                var end = start.AddMinutes(durationMin);
                if (end <= rangeStart || start >= rangeEnd)
                    continue;

                results.Add((start, end));
            }
        }

        return results.OrderBy(r => r.Start).ToList();
    }

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "InPerson",
        LessonMode.Hybrid => "Hybrid",
        _ => "Online"
    };
}
