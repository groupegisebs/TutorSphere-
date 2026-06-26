namespace TutorSphere.Application.DTOs.SubscriptionOfferings;

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
    bool IsActive);

public record CreateSubscriptionOfferingRequest(
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency);

public record UpdateSubscriptionOfferingRequest(
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency);
