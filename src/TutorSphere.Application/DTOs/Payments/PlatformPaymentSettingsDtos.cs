namespace TutorSphere.Application.DTOs.Payments;

public record PlatformPaymentSettingsDto(
    decimal DefaultCommissionPercent,
    decimal CardFeePercent,
    decimal CardFeeFixed,
    decimal PayPalFeePercent,
    decimal PayPalFeeFixed);

public record UpdatePlatformPaymentSettingsRequest(
    decimal DefaultCommissionPercent,
    decimal CardFeePercent,
    decimal CardFeeFixed,
    decimal PayPalFeePercent,
    decimal PayPalFeeFixed);
