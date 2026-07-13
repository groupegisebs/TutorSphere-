using TutorSphere.Application.DTOs.TutorPayouts;

namespace TutorSphere.Application.Common.Interfaces;

/// <summary>
/// Décaissements tuteur via GiseBs PayGateway (file d'attente + rapprochement admin avant paiement).
/// </summary>
public interface ITutorDisbursementGateway
{
    bool IsConfigured { get; }

    Task<TutorDisbursementEnqueueResult> EnqueueAsync(TutorDisbursementEnqueueRequest request, CancellationToken ct = default);

    Task<TutorConnectOnboardingResult> StartStripeConnectOnboardingAsync(
        string externalReference,
        string countryCode,
        string currency,
        string? email,
        string returnUrl,
        string refreshUrl,
        CancellationToken ct = default);

    Task<TutorConnectAccountStatus?> GetStripeConnectAccountAsync(string externalAccountId, CancellationToken ct = default);

    Task<TutorPayPalOAuthStart?> StartPayPalOAuthAsync(string externalReference, string? returnUrl, CancellationToken ct = default);

    Task<TutorPayPalLinkedAccount?> GetPayPalAccountAsync(string externalReference, CancellationToken ct = default);

    Task<TutorMobileMoneyValidation> ValidateMobileMoneyAsync(
        string countryCode,
        string operatorCode,
        string phone,
        string holderName,
        CancellationToken ct = default);

    Task RegisterMobileMoneyRecipientAsync(
        string externalReference,
        string countryCode,
        string operatorCode,
        string phone,
        string holderName,
        CancellationToken ct = default);
}

public record TutorDisbursementEnqueueRequest(
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

public record TutorDisbursementEnqueueResult(
    Guid Id,
    string Status,
    string ExternalReference,
    string IdempotencyKey);

public record TutorConnectOnboardingResult(string ExternalAccountId, string OnboardingUrl, string Status);

public record TutorConnectAccountStatus(
    string ExternalAccountId,
    string Status,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    string? MaskedEmail);

public record TutorPayPalOAuthStart(string AuthorizationUrl, string State);

public record TutorPayPalLinkedAccount(string ExternalReference, string? MaskedEmail, string Status, string? PayerId);

public record TutorMobileMoneyValidation(bool IsValid, string? MaskedPhone, string? ExternalToken, string? Message);
