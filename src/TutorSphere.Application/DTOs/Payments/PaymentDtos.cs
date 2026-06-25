namespace TutorSphere.Application.DTOs.Payments;

public record PaymentGatewayConfigDto(string? PublishableKey);

public record CreateSubscriptionCheckoutRequest(string SuccessUrl, string CancelUrl);

public record ParentCustomerResponse(Guid ParentProfileId, string CustomerCode);

public record SubscriptionCheckoutResponse(
    Guid PaymentId,
    string PaymentCode,
    string CheckoutUrl,
    string SessionId,
    string? ClientSecret,
    decimal Amount,
    decimal PlatformFee,
    decimal TutorAmount,
    string Currency);

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
