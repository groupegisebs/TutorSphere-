using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.SubscriptionOfferings;

public record OfferingScheduleSlotDto(string Day, string Time);

public record OfferingScheduleDto(
    string BillingPeriod,
    string Cadence,
    int SessionDurationMin,
    string? Level,
    string? CancellationPolicy,
    IReadOnlyList<OfferingScheduleSlotDto> Slots,
    decimal? HourlyRate = null);

public record SubscriptionOfferingDto(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency,
    bool IsActive,
    string Mode,
    string? Conditions,
    OfferingScheduleDto? Schedule,
    int ActiveSubscribers = 0,
    decimal MonthlyRevenue = 0,
    int MaxCapacity = 20);

public record CreateSubscriptionOfferingRequest(
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency,
    string? Mode = null,
    string? Conditions = null,
    OfferingScheduleDto? Schedule = null,
    int MaxCapacity = 20);

public record UpdateSubscriptionOfferingRequest(
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency,
    string? Mode = null,
    string? Conditions = null,
    OfferingScheduleDto? Schedule = null,
    int MaxCapacity = 20);
