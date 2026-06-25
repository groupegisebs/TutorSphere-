namespace TutorSphere.Application.DTOs.Payments;

public record StripeConfigDto(string PublishableKey);

public record ConnectOnboardingRequest(string ReturnUrl, string RefreshUrl);

public record ConnectOnboardingResponse(string AccountId, string OnboardingUrl);

public record ConnectAccountStatusResponse(
    string AccountId,
    bool ChargesEnabled,
    bool DetailsSubmitted,
    bool PayoutsEnabled);

public record ParentCustomerResponse(Guid ParentProfileId, string StripeCustomerId);

public record SubscriptionPaymentIntentResponse(
    Guid PaymentId,
    string ClientSecret,
    string PaymentIntentId,
    decimal Amount,
    decimal PlatformFee,
    decimal TutorAmount,
    string Currency);
