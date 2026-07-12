using TutorSphere.Application.DTOs.Payments;

namespace TutorSphere.Application.Common.Interfaces;

public interface IPaymentGatewayService
{
    PaymentGatewayConfigDto GetConfig();

    Task<ParentCustomerResponse> CreateOrGetParentCustomerAsync(
        Guid parentProfileId,
        CancellationToken ct = default);

    Task<SubscriptionCheckoutResponse> CreateSubscriptionCheckoutAsync(
        Guid subscriptionId,
        CreateSubscriptionCheckoutRequest request,
        CancellationToken ct = default);

    Task<PaymentStatusResponse> SyncPaymentStatusAsync(
        Guid paymentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<GatewaySubscriptionResponse>> GetParentSubscriptionsAsync(
        Guid parentProfileId,
        CancellationToken ct = default);

    Task<CancelSubscriptionResponse> CancelSubscriptionAsync(
        Guid subscriptionId,
        bool cancelImmediately,
        CancellationToken ct = default);

    /// <summary>
    /// Crée / met à jour le produit+plan dans Pay Gateway et Stripe (SyncToStripe).
    /// </summary>
    Task SyncOfferingCatalogAsync(Guid offeringId, CancellationToken ct = default);
}
