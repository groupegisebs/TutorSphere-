namespace TutorSphere.Application.DTOs.Payments;

public record PlatformPaymentSettingsDto(
    decimal DefaultCommissionPercent,
    decimal CardFeePercent,
    decimal CardFeeFixed,
    decimal PayPalFeePercent,
    decimal PayPalFeeFixed,
    decimal MobileMoneyFeePercent = 2.0m,
    decimal MobileMoneyFeeFixed = 0m);

public record UpdatePlatformPaymentSettingsRequest(
    decimal DefaultCommissionPercent,
    decimal CardFeePercent,
    decimal CardFeeFixed,
    decimal PayPalFeePercent,
    decimal PayPalFeeFixed,
    decimal MobileMoneyFeePercent = 2.0m,
    decimal MobileMoneyFeeFixed = 0m);
