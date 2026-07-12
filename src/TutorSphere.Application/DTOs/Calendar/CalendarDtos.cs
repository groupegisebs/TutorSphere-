namespace TutorSphere.Application.DTOs.Calendar;

public enum CalendarView
{
    Day,
    Week,
    Month
}

public record CreateUnavailabilityRequest(
    string? Reason,
    DateTime StartTime,
    DateTime EndTime);

public record UpdateUnavailabilityRequest(
    string? Reason,
    DateTime StartTime,
    DateTime EndTime);

public record UnavailabilityDto(
    Guid Id,
    string? Reason,
    DateTime StartTime,
    DateTime EndTime,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateHolidayRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate);

public record UpdateHolidayRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate);

public record HolidayDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateVacationRequest(
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate);

public record UpdateVacationRequest(
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate);

public record VacationDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CalendarViewDto(
    DateTime RangeStart,
    DateTime RangeEnd,
    IReadOnlyList<Lessons.LessonDto> Lessons,
    IReadOnlyList<UnavailabilityDto> Unavailabilities,
    IReadOnlyList<HolidayDto> Holidays,
    IReadOnlyList<VacationDto> Vacations,
    IReadOnlyList<OfferAvailabilityDto> OfferAvailabilities);

/// <summary>Créneau récurrent d'une offre active, projeté sur la plage du calendrier.</summary>
public record OfferAvailabilityDto(
    Guid OfferingId,
    string Title,
    string? Subject,
    DateTime StartTime,
    DateTime EndTime,
    string Mode,
    string Cadence);
