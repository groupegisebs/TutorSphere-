using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.PayGateway;

internal sealed class TutorDisbursementGateway(
    PayGatewayClient client,
    IOptions<PayGatewaySettings> settings) : ITutorDisbursementGateway
{
    public bool IsConfigured => client.IsConfigured
        && !string.IsNullOrWhiteSpace(settings.Value.BaseUrl);

    public async Task<TutorDisbursementEnqueueResult> EnqueueAsync(
        TutorDisbursementEnqueueRequest request, CancellationToken ct = default)
    {
        var response = await client.EnqueueDisbursementAsync(new GatewayEnqueueDisbursementRequest(
            request.ExternalReference,
            request.IdempotencyKey,
            request.SellerExternalId,
            request.SellerDisplayName,
            request.ProviderCode,
            request.DestinationMasked,
            request.DestinationToken,
            request.AmountMinor,
            request.Currency.ToLowerInvariant(),
            request.CountryCode), ct);

        return new TutorDisbursementEnqueueResult(
            response.Id,
            response.Status,
            response.ExternalReference,
            response.IdempotencyKey);
    }

    public async Task<TutorConnectOnboardingResult> StartStripeConnectOnboardingAsync(
        string externalReference,
        string countryCode,
        string currency,
        string? email,
        string returnUrl,
        string refreshUrl,
        CancellationToken ct = default)
    {
        var account = await client.CreateConnectAccountAsync(new GatewayCreateConnectAccountRequest(
            externalReference,
            countryCode,
            currency.ToLowerInvariant(),
            email), ct);

        var link = await client.CreateAccountLinkAsync(new GatewayCreateAccountLinkRequest(
            account.ExternalAccountId, returnUrl, refreshUrl), ct);

        return new TutorConnectOnboardingResult(account.ExternalAccountId, link.Url, account.Status);
    }

    public async Task<TutorConnectAccountStatus?> GetStripeConnectAccountAsync(
        string externalAccountId, CancellationToken ct = default)
    {
        var account = await client.GetConnectAccountAsync(externalAccountId, ct);
        return account is null
            ? null
            : new TutorConnectAccountStatus(
                account.ExternalAccountId,
                account.Status,
                account.PayoutsEnabled,
                account.DetailsSubmitted,
                account.MaskedEmail);
    }

    public async Task<TutorPayPalOAuthStart?> StartPayPalOAuthAsync(
        string externalReference, string? returnUrl, CancellationToken ct = default)
    {
        var result = await client.StartPayPalOAuthAsync(
            new GatewayPayPalOAuthStartRequest(externalReference, returnUrl), ct);
        return new TutorPayPalOAuthStart(result.AuthorizationUrl, result.State);
    }

    public async Task<TutorPayPalLinkedAccount?> GetPayPalAccountAsync(
        string externalReference, CancellationToken ct = default)
    {
        var linked = await client.GetPayPalAccountAsync(externalReference, ct);
        return linked is null
            ? null
            : new TutorPayPalLinkedAccount(linked.ExternalReference, linked.MaskedEmail, linked.Status, linked.PayerId);
    }

    public async Task<TutorMobileMoneyValidation> ValidateMobileMoneyAsync(
        string countryCode, string operatorCode, string phone, string holderName, CancellationToken ct = default)
    {
        var result = await client.ValidateMobileMoneyAsync(new GatewayMobileMoneyValidateRequest(
            countryCode, operatorCode, phone, holderName), ct);
        return new TutorMobileMoneyValidation(result.IsValid, result.MaskedPhone, result.ExternalToken, result.Message);
    }

    public Task RegisterMobileMoneyRecipientAsync(
        string externalReference,
        string countryCode,
        string operatorCode,
        string phone,
        string holderName,
        CancellationToken ct = default)
        => client.RegisterMobileMoneyRecipientAsync(new GatewayRegisterMobileMoneyRequest(
            externalReference, countryCode, operatorCode, phone, holderName), ct);
}
