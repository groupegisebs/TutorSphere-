namespace TutorSphere.Application.DTOs.Payments;

public static class PaymentMethodCodes
{
    public const string Card = "card";
    public const string PayPal = "paypal";
    public const string MobileMoney = "mobile_money";

    public static string Normalize(string? value) =>
        (value ?? Card).Trim().ToLowerInvariant() switch
        {
            "paypal" or "pp" => PayPal,
            "mobile_money" or "mobilemoney" or "momo" or "flutterwave" => MobileMoney,
            _ => Card
        };
}

public record PaymentGatewayConfigDto(string? PublishableKey);

public record CreateSubscriptionCheckoutRequest(
    string SuccessUrl,
    string CancelUrl,
    string? PaymentMethod = PaymentMethodCodes.Card,
    string? CountryCode = null,
    string? Network = null,
    string? PhoneNumber = null);

public record ParentCustomerResponse(Guid ParentProfileId, string CustomerCode);

public record SubscriptionCheckoutResponse(
    Guid PaymentId,
    string PaymentCode,
    string? CheckoutUrl,
    string? SessionId,
    string? ClientSecret,
    decimal Amount,
    decimal PlatformFee,
    decimal TutorAmount,
    string Currency,
    string PaymentMethod,
    string? Instruction = null,
    string? RedirectUrl = null,
    string? Message = null);

public record PaymentStatusResponse(
    Guid PaymentId,
    string PaymentCode,
    string GatewayStatus,
    string LocalStatus,
    DateTime? PaidAt);

public record GatewaySubscriptionResponse(
    string SubscriptionCode,
    string Status,
    string ProductCode,
    string PlanCode,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd);

public record CancelSubscriptionResponse(
    string SubscriptionCode,
    string Status,
    DateTime? CancelledAt);

/// <summary>Ligne d'historique paiements / factures côté parent.</summary>
public record ParentPaymentDto(
    Guid Id,
    Guid? InvoiceId,
    string? InvoiceNumber,
    string Description,
    string? StudentName,
    string? TutorName,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt,
    bool CanDownloadInvoice);

public record MobileMoneyCountryDto(
    string CountryCode,
    string CountryName,
    string Currency,
    string PhoneCountryCode,
    IReadOnlyList<MobileMoneyNetworkOptionDto> Networks);

public record MobileMoneyNetworkOptionDto(string Network, string NetworkLabel);

public record MobileMoneyNetworkDto(
    string CountryCode,
    string CountryName,
    string Currency,
    string Network,
    string NetworkLabel,
    string PhoneCountryCode);

public record MobileMoneyQuoteDto(
    decimal OriginalAmount,
    string OriginalCurrency,
    decimal Amount,
    string Currency,
    string CountryCode,
    string CountryName);
