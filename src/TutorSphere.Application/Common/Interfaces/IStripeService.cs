using TutorSphere.Application.DTOs.Payments;

namespace TutorSphere.Application.Common.Interfaces;

public interface IStripeService
{
    StripeConfigDto GetConfig();

    Task<ConnectOnboardingResponse> CreateConnectOnboardingAsync(
        Guid tenantId,
        ConnectOnboardingRequest request,
        CancellationToken ct = default);

    Task<ConnectAccountStatusResponse> GetConnectAccountStatusAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<ParentCustomerResponse> CreateOrGetParentCustomerAsync(
        Guid parentProfileId,
        CancellationToken ct = default);

    Task<SubscriptionPaymentIntentResponse> CreateSubscriptionPaymentIntentAsync(
        Guid subscriptionId,
        CancellationToken ct = default);

    Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default);
}
