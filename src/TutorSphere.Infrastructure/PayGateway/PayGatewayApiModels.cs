namespace TutorSphere.Infrastructure.PayGateway;

internal sealed record GatewayApiError(string Error, string? Details);

internal sealed record GatewayCheckoutSessionRequest(
    string CustomerCode,
    string Email,
    string? FullName,
    string? ExternalUserId,
    string ProductCode,
    string PlanCode,
    string SuccessUrl,
    string CancelUrl,
    string? MetadataJson,
    int? TrialDays,
    bool Embedded = false,
    IReadOnlyList<string>? PaymentMethodTypes = null);

internal sealed record GatewayCheckoutSessionResponse(
    string PaymentCode,
    string CheckoutUrl,
    string SessionId,
    string Status,
    string? ClientSecret = null,
    string? PublishableKey = null,
    /// <summary>Mode Stripe réellement utilisé par Pay Gateway : <c>PROD</c> ou <c>DEV</c>.</summary>
    string? StripeMode = null);

internal sealed record GatewayPaymentResponse(
    string PaymentCode,
    string Status,
    decimal Amount,
    string Currency,
    string CustomerCode,
    string ProductCode,
    string PlanCode,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? FailureReason,
    string? StripeCheckoutSessionId,
    string? StripePaymentIntentId);

internal sealed record GatewayCreateCatalogItemRequest(
    string ProductCode,
    string ProductName,
    string? Description,
    string PlanCode,
    string PlanName,
    decimal Amount,
    string Currency,
    string? BillingInterval = null,
    bool SyncToStripe = true);

internal sealed record GatewayProductResponse(
    string ProductCode,
    string Name,
    string? Description,
    bool IsActive,
    string? StripeProductId,
    DateTime CreatedAt,
    IReadOnlyList<GatewayPricingPlanResponse>? Plans = null);

internal sealed record GatewayPricingPlanResponse(
    string PlanCode,
    string Name,
    decimal Amount,
    string Currency,
    string BillingInterval,
    bool IsActive,
    string? StripePriceId,
    DateTime CreatedAt);

internal sealed record GatewayApiSubscriptionResponse(
    string SubscriptionCode,
    string Status,
    string CustomerCode,
    string ProductCode,
    string PlanCode,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd);

internal sealed record GatewayCancelSubscriptionRequest(string SubscriptionCode, bool CancelImmediately);

internal sealed record GatewayCancelSubscriptionResponse(
    string SubscriptionCode,
    string Status,
    DateTime? CancelledAt);

internal sealed record GatewayCreateConnectAccountRequest(
    string ExternalReference,
    string CountryCode,
    string DefaultCurrency,
    string? Email,
    string AccountType = "express");

internal sealed record GatewayConnectAccountResponse(
    string ExternalAccountId,
    string ExternalReference,
    string Country,
    string Currency,
    string? MaskedEmail,
    string Status,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted);

internal sealed record GatewayCreateAccountLinkRequest(
    string ExternalAccountId,
    string ReturnUrl,
    string RefreshUrl);

internal sealed record GatewayAccountLinkResponse(
    string ExternalAccountId,
    string Url,
    DateTime ExpiresAt);

internal sealed record GatewayEnqueueDisbursementRequest(
    string ExternalReference,
    string IdempotencyKey,
    string SellerExternalId,
    string? SellerDisplayName,
    string ProviderCode,
    string DestinationMasked,
    string? DestinationToken,
    long AmountMinor,
    string Currency,
    string CountryCode);

internal sealed record GatewayDisbursementResponse(
    Guid Id,
    string ExternalReference,
    string IdempotencyKey,
    string ProviderCode,
    string DestinationMasked,
    long AmountMinor,
    string Currency,
    string CountryCode,
    string Status,
    bool ReconciliationChecked,
    string? ProviderPayoutId,
    string? FailureMessage);

internal sealed record GatewayPayPalOAuthStartRequest(string ExternalReference, string? ReturnUrl);
internal sealed record GatewayPayPalOAuthStartResponse(string AuthorizationUrl, string State);
internal sealed record GatewayPayPalAccountResponse(string ExternalReference, string? MaskedEmail, string Status, string? PayerId);

internal sealed record GatewayMobileMoneyValidateRequest(
    string CountryCode,
    string OperatorCode,
    string PhoneNumber,
    string AccountHolderName);

internal sealed record GatewayMobileMoneyValidateResponse(
    bool IsValid,
    string? MaskedPhone,
    string? ExternalToken,
    string? Message);

internal sealed record GatewayRegisterMobileMoneyRequest(
    string ExternalReference,
    string CountryCode,
    string OperatorCode,
    string PhoneNumber,
    string AccountHolderName);

// ── Collecte Mobile Money ────────────────────────────────────────────────────

internal sealed record GatewayMobileMoneyChargeRequest(
    string CustomerCode,
    string Email,
    string? FullName,
    string? ExternalUserId,
    string ProductCode,
    string PlanCode,
    string Channel,
    string? PhoneNumber = null,
    string? BillingCountryCode = null,
    string? MetadataJson = null,
    string? Description = null,
    string? ReturnUrl = null,
    string? CancelUrl = null);

internal sealed record GatewayMobileMoneyChargeResponse(
    string PaymentCode,
    string Status,
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
    string? BillingCountryCode = null,
    string? PaymentUrl = null);

internal sealed record GatewayMobileMoneyStatusResponse(
    string PaymentCode,
    string Status,
    string? RawProviderStatus,
    decimal Amount,
    string Currency,
    string? Channel,
    string? PhoneMasked,
    string? ProviderReference,
    DateTime? PaidAt,
    DateTime? ExpiresAtUtc,
    string? FailureCode,
    string? FailureReason,
    decimal? AmountExclusive = null,
    decimal? TaxAmount = null,
    decimal? TaxRatePercent = null,
    string? TaxName = null,
    string? BillingCountryCode = null);

internal sealed record GatewayAfricanTaxQuoteRequest(
    decimal AmountExclusive,
    string Currency,
    string CountryCode);

internal sealed record GatewayAfricanTaxQuoteResponse(
    string CountryCode,
    string CountryName,
    string TaxName,
    decimal TaxRatePercent,
    decimal AmountExclusive,
    decimal TaxAmount,
    decimal AmountInclusive,
    string Currency);

internal sealed record AfricanTaxRateDtoLite(
    string CountryCode,
    string CountryName,
    string TaxName,
    decimal RatePercent,
    string? Notes);
