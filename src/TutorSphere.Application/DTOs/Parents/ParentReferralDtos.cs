namespace TutorSphere.Application.DTOs.Parents;

public record ParentReferralDto(
    string Code,
    string ShareUrl,
    int RewardMonthsAvailable,
    int SuccessfulReferrals,
    string OfferSummary);

public record ParentSupportRequestDto(
    Guid Id,
    string Subject,
    string Message,
    int Status,
    DateTime CreatedAt);

public record CreateParentSupportRequest(
    string Subject,
    string Message,
    string? ContactEmail = null);
