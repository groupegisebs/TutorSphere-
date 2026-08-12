namespace TutorSphere.Application.DTOs.Payments;

public static class PaymentMethodCodes
{
    public const string Card = "card";
    public const string PayPal = "paypal";

    public static string Normalize(string? value) =>
        (value ?? Card).Trim().ToLowerInvariant() switch
        {
            "paypal" or "pp" => PayPal,
            _ => Card
        };
}

public record PaymentGatewayConfigDto(string? PublishableKey);

public record CreateSubscriptionCheckoutRequest(
    string SuccessUrl,
    string CancelUrl,
    string? PaymentMethod = PaymentMethodCodes.Card);

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

