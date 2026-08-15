using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.DTOs.PlatformBilling;

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

    /// <summary>
    /// Après retour Checkout : interroge Pay Gateway (avec retries) pour le dernier paiement
    /// de l'abonnement et active l'abonnement local si Succeeded — même modèle que Boutique/AGENTIA.
    /// </summary>
    Task<PaymentStatusResponse> ConfirmSubscriptionPaymentAsync(
        Guid subscriptionId,
        int maxAttempts = 5,
        int retryDelayMs = 2000,
        CancellationToken ct = default);

    Task<IReadOnlyList<GatewaySubscriptionResponse>> GetParentSubscriptionsAsync(
        Guid parentProfileId,
        CancellationToken ct = default);

    Task<CancelSubscriptionResponse> CancelSubscriptionAsync(
        Guid subscriptionId,
        bool cancelImmediately,
        CancellationToken ct = default);

    /// <summary>
    /// Annule l'abonnement passerelle sans filtre tenant. Ignore si non lié / passerelle indisponible.
    /// </summary>
    Task TryCancelGatewaySubscriptionAsync(
        Guid subscriptionId,
        bool cancelImmediately,
        CancellationToken ct = default);

    /// <summary>
    /// Rembourse un paiement parent déjà encaissé (Stripe via Pay Gateway).
    /// Mobile Money : marquage local + e-mail si la passerelle ne supporte pas le refund.
    /// </summary>
    Task RefundCompletedPaymentAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// Vérifie que l'appelant (parent de l'élève, élève autonome, tuteur propriétaire, admin) peut payer ce forfait.
    /// </summary>
    Task AssertUserCanPaySubscriptionAsync(string userId, Guid subscriptionId, CancellationToken ct = default);

    /// <summary>Interroge la passerelle pour tous les paiements Pending encore liés à un code passerelle.</summary>
    Task<int> SyncPendingPaymentsAsync(CancellationToken ct = default);

    /// <summary>Webhook Pay Gateway : active le pack à partir du code de paiement passerelle.</summary>
    Task SyncPaymentByGatewayCodeAsync(string paymentCode, CancellationToken ct = default);

    /// <summary>
    /// Crée / met à jour le produit+plan dans Pay Gateway et Stripe (SyncToStripe).
    /// </summary>
    Task SyncOfferingCatalogAsync(Guid offeringId, CancellationToken ct = default);

    Task<PlatformLicenseCheckoutResponse> CreatePlatformLicenseCheckoutAsync(
        Guid tenantId,
        CreatePlatformLicenseCheckoutRequest request,
        CancellationToken ct = default);

    Task<PaymentStatusResponse> ConfirmPlatformLicensePaymentAsync(
        Guid tenantId,
        Guid? paymentId = null,
        int maxAttempts = 5,
        int retryDelayMs = 2000,
        CancellationToken ct = default);

    Task<MobileMoneyChargeResponse> CreateMobileMoneyChargeAsync(
        CreateMobileMoneyChargeRequest request,
        CancellationToken ct = default);

    Task<AfricanTaxQuoteDto> QuoteAfricanTaxAsync(
        decimal amountExclusive,
        string currency,
        string countryCode,
        CancellationToken ct = default);

    Task<IReadOnlyList<AfricanTaxRateDto>> ListAfricanTaxRatesAsync(CancellationToken ct = default);
}
