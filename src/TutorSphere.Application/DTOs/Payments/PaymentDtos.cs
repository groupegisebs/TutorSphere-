namespace TutorSphere.Application.DTOs.Payments;

public static class PaymentMethodCodes
{
    public const string Card = "card";
    public const string PayPal = "paypal";
    public const string OrangeMoney = "orange";
    public const string MtnMomo = "mtn";

    public static string Normalize(string? value) =>
        (value ?? Card).Trim().ToLowerInvariant() switch
        {
            "paypal" or "pp" => PayPal,
            "orange" or "orange_money" or "om" => OrangeMoney,
            "mtn" or "mtn_momo" or "momo" => MtnMomo,
            _ => Card
        };

    public static bool IsMobileMoney(string? value)
    {
        var n = Normalize(value);
        return n is OrangeMoney or MtnMomo;
    }
}

public record PaymentGatewayConfigDto(string? PublishableKey);

public record CreateSubscriptionCheckoutRequest(
    string SuccessUrl,
    string CancelUrl,
    string? PaymentMethod = PaymentMethodCodes.Card);

public record CreateMobileMoneyChargeRequest(
    Guid SubscriptionId,
    string Channel,
    string PhoneNumber,
    string? IdempotencyKey = null,
    /// <summary>Code ISO pays du payeur (ex. CM). Défaut CM pour Mobile Money Cameroun.</summary>
    string? BillingCountryCode = null);

public record MobileMoneyChargeResponse(
    Guid PaymentId,
    string PaymentCode,
    string Status,
    /// <summary>Montant TTC encaissé.</summary>
    decimal Amount,
    string Currency,
    string Channel,
    string PhoneMasked,
    string? ProviderReference,
    DateTime? ExpiresAtUtc,
    string? Instruction,
    string? UssdHint,
    decimal AmountExclusive = 0,
    decimal TaxAmount = 0,
    decimal TaxRatePercent = 0,
    string? TaxName = null,
    string? BillingCountryCode = null);

public record AfricanTaxQuoteDto(
    string CountryCode,
    string CountryName,
    string TaxName,
    decimal TaxRatePercent,
    decimal AmountExclusive,
    decimal TaxAmount,
    decimal AmountInclusive,
    string Currency);

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
    string PaymentMethod);

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
