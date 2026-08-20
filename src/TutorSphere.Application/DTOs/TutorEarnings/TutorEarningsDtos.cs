using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.TutorEarnings;

/// <param name="OtherCurrencyEarnings">
/// Gains encaissés hors devise de versement. Ils ne sont pas réclamables tant qu'aucune conversion
/// n'existe, mais l'enseignant doit les voir plutôt que de les chercher.
/// </param>
public record TutorEarningsSummaryDto(
    decimal Collected,
    decimal Held,
    decimal Released,
    decimal Withdrawn,
    decimal Available,
    string Currency,
    int SessionsHeld,
    IReadOnlyList<TutorPayoutDto> RecentPayouts,
    PayoutEligibilityDto? Eligibility,
    IReadOnlyList<MoneyTotal>? OtherCurrencyEarnings = null);

public record TutorPayoutDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    string? Note,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    string? ProviderKind,
    Guid? PayoutAccountId,
    string? InvoiceNumber = null,
    string? TeacherName = null,
    string? PaymentMethodSummary = null,
    Guid? ExpertGroupId = null);

public record RequestTutorPayoutRequest(
    decimal? Amount,
    string? Note);

public record TutorPayoutStatusNames
{
    public static string Of(TutorPayoutStatus status) => status switch
    {
        TutorPayoutStatus.Pending => "pending",
        TutorPayoutStatus.Processing => "processing",
        TutorPayoutStatus.Completed => "completed",
        TutorPayoutStatus.Failed => "failed",
        TutorPayoutStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant()
    };
}
