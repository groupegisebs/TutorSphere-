using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.SubscriptionOfferings;

public record OfferingScheduleSlotDto(string Day, string Time, string? EndTime = null);

public record OfferingScheduleDto(
    string BillingPeriod,
    string Cadence,
    int SessionDurationMin,
    string? Level,
    string? CancellationPolicy,
    IReadOnlyList<OfferingScheduleSlotDto> Slots,
    decimal? HourlyRate = null,
    /// <summary>hourly | monthly | perSession | quarterlyPerValidated — facturation après validation du cours.</summary>
    string? BillingMode = null,
    decimal? SessionRate = null,
    /// <summary>URL de la vidéo de présentation du cours (document uploadé ou lien).</summary>
    string? PresentationVideoUrl = null,
    /// <summary>Heures de cours par semaine (surtout pour le taux mensuel).</summary>
    decimal? HoursPerWeek = null,
    /// <summary>IANA (ex. Africa/Abidjan). Les plages sont exprimées dans ce fuseau.</summary>
    string? TimeZone = null);

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
