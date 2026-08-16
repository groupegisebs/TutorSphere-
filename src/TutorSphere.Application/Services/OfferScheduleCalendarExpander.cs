using System.Text.Json;
using System.Text.Json.Serialization;
using TutorSphere.Application.Common;
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
                if (!AvailabilityWindows.TryParseDay(slot.Day, out var dayOfWeek))
                    continue;
                if (!AvailabilityWindows.TryParseTime(slot.Time, out var startTime))
                    continue;

                var (winStart, winEnd) = SlotPeriod(slot, startTime, durationMin);

                for (var day = startDate; day < rangeEnd; day = day.AddDays(1))
                {
                    if (day.DayOfWeek != dayOfWeek)
                        continue;
                    if (!MatchesCadence(day, cadence, cadenceAnchor))
                        continue;

                    var start = DateTime.SpecifyKind(day.Add(winStart), DateTimeKind.Unspecified);
                    var end = DateTime.SpecifyKind(day.Add(winEnd), DateTimeKind.Unspecified);
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

    /// <summary>
    /// Période telle que déclarée dans l'offre (une plage = un bloc d'agenda).
    /// Ancien format (heure seule) : la plage vaut la durée d'une séance.
    /// </summary>
    private static (TimeSpan Start, TimeSpan End) SlotPeriod(
        OfferingScheduleSlotDto slot,
        TimeSpan startTime,
        int durationMin)
    {
        if (AvailabilityWindows.TryParseTime(slot.EndTime, out var endTime) && endTime > startTime)
            return (startTime, endTime);

        return (startTime, startTime.Add(TimeSpan.FromMinutes(durationMin)));
    }

    /// <summary>
    /// Plage stockée : créneaux de réservation générés dynamiquement.
    /// Ancien format (heure seule) : un créneau de la durée de séance.
    /// </summary>
    private static IReadOnlyList<(TimeSpan Start, TimeSpan End)> SlotWindows(
        OfferingScheduleSlotDto slot,
        TimeSpan startTime,
        int durationMin)
    {
        if (AvailabilityWindows.TryParseTime(slot.EndTime, out var endTime) && endTime > startTime)
            return AvailabilityWindows.ToBookingSlots(startTime, endTime, durationMin);

        return [(startTime, startTime.Add(TimeSpan.FromMinutes(durationMin)))];
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
            if (!AvailabilityWindows.TryParseDay(slot.Day, out var dayOfWeek))
                continue;
            if (!AvailabilityWindows.TryParseTime(slot.Time, out var startTime))
                continue;

            var windows = SlotWindows(slot, startTime, durationMin);
            if (windows.Count == 0)
                continue;

            for (var day = startDate; day < rangeEnd; day = day.AddDays(1))
            {
                if (day.DayOfWeek != dayOfWeek)
                    continue;
                if (!MatchesCadence(day, cadence, cadenceAnchor))
                    continue;

                foreach (var (winStart, winEnd) in windows)
                {
                    var start = DateTime.SpecifyKind(day.Add(winStart), DateTimeKind.Unspecified);
                    var end = DateTime.SpecifyKind(day.Add(winEnd), DateTimeKind.Unspecified);
                    if (end <= rangeStart || start >= rangeEnd)
                        continue;

                    results.Add((start, end));
                }
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
