using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.TutorPayouts;

public record TutorPayoutAccountDto(
    Guid Id,
    string Label,
    string ProviderKind,
    string CountryCode,
    string Currency,
    bool IsPrimary,
    bool IsActive,
    string AccountHolderName,
    string? EmailOrAccountId,
    string? PhoneNumber,
    string? PaymentDetails,
    bool IsVerified,
    DateTime? VerifiedAt);

public record UpsertTutorPayoutAccountRequest(
    string Label,
    string ProviderKind,
    string? CountryCode,
    string? Currency,
    bool IsPrimary,
    string AccountHolderName,
    string? EmailOrAccountId,
    string? PhoneNumber,
    string? PaymentDetails);

public record TutorPayoutSetupDto(
    string Region,
    string CountryCode,
    string Currency,
    IReadOnlyList<string> RequiredProviders,
    bool StripeConfigured,
    bool PayPalConfigured,
    bool SetupComplete,
    string? StripeAccountId,
    string? PayPalEmail,
    IReadOnlyList<TutorPayoutAccountDto> Accounts,
    IReadOnlyList<PayoutProviderCatalogItemDto> Catalog);

public record PayoutProviderCatalogItemDto(
    string ProviderKind,
    string DisplayName,
    bool IsRequired,
    string Region);

public record UpdateTutorPayoutProfileRequest(
    string? Country,
    string? PayPalEmail,
    string? StripeAccountId);

public record PayoutEligibilityDto(
    bool CanWithdraw,
    decimal Available,
    decimal ClaimableNow,
    decimal MinimumTransfer,
    decimal InstantThreshold,
    int HoldingDaysRequired,
    int? HoldingDaysElapsed,
    int? HoldingDaysRemaining,
    DateTime? HoldingStartedAt,
    DateTime? EligibleAt,
    bool SetupComplete,
    string? BlockReason,
    string Currency);

public record TutorPayoutMethodSnapshotDto(
    string ProviderKind,
    string Label,
    string AccountHolderName,
    string CountryCode,
    string Currency,
    string? EmailOrAccountId,
    string? PhoneNumber,
    string? PaymentDetails,
    bool IsPrimary);

public record GroupTeacherPayoutInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid TenantId,
    string TeacherName,
    decimal Amount,
    string Currency,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessingAt,
    DateTime? CompletedAt,
    TutorPayoutMethodSnapshotDto PaymentMethod,
    string? Note);
