namespace TutorSphere.Application.DTOs.PlatformBilling;

public record PlatformLicenseStatusDto(
    Guid TenantId,
    string SchoolName,
    string Status,
    bool HasValidLicense,
    bool HasPaidLicense,
    bool RequiresPayment,
    bool RequiresOnboarding,
    DateTime? LicenseExpiresAt,
    DateTime? OnboardingCompletedAt,
    int? DaysUntilExpiry,
    decimal AnnualFee,
    string Currency,
    bool RenewalSoon);

public record CreatePlatformLicenseCheckoutRequest(
    string SuccessUrl,
    string CancelUrl,
    string? PaymentMethod = "card",
    string? CountryCode = null,
    string? Network = null,
    string? PhoneNumber = null);

public record PlatformLicenseCheckoutResponse(
    Guid PaymentId,
    string PaymentCode,
    string? CheckoutUrl,
    string? SessionId,
    string? ClientSecret,
    decimal Amount,
    string Currency,
    string PaymentMethod = "card",
    string? Instruction = null,
    string? RedirectUrl = null,
    string? Message = null);

public record PlatformLicensePaymentStatusDto(
    Guid PaymentId,
    string PaymentCode,
    string GatewayStatus,
    string LocalStatus,
    DateTime? PaidAt,
    DateTime? LicenseExpiresAt,
    bool HasValidLicense,
    bool HasPaidLicense,
    bool RequiresOnboarding);
