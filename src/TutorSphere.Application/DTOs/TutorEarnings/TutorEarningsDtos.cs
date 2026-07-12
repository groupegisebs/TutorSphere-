using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.TutorEarnings;

public record TutorEarningsSummaryDto(
    decimal Collected,
    decimal Held,
    decimal Released,
    decimal Withdrawn,
    decimal Available,
    string Currency,
    int SessionsHeld,
    IReadOnlyList<TutorPayoutDto> RecentPayouts,
    PayoutEligibilityDto? Eligibility);

public record TutorPayoutDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string? Note,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    string? ProviderKind,
    Guid? PayoutAccountId);

public record RequestTutorPayoutRequest(
    decimal? Amount,
    string? Note);

public record TutorPayoutStatusNames
{
    public static string Of(TutorPayoutStatus status) => status switch
    {
        TutorPayoutStatus.Pending => "pending",
        TutorPayoutStatus.Completed => "completed",
        TutorPayoutStatus.Failed => "failed",
        TutorPayoutStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant()
    };
}
