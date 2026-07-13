using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.Application.Services;

public interface ITutorPayoutAccountService
{
    Task<TutorPayoutSetupDto> GetSetupAsync(CancellationToken ct = default);
    Task<TutorPayoutSetupDto> UpdateProfileAsync(UpdateTutorPayoutProfileRequest request, CancellationToken ct = default);
    Task<TutorPayoutAccountDto> UpsertAccountAsync(UpsertTutorPayoutAccountRequest request, Guid? id = null, CancellationToken ct = default);
    Task SetPrimaryAsync(Guid accountId, CancellationToken ct = default);
    Task DeactivateAsync(Guid accountId, CancellationToken ct = default);
    Task<TutorConnectOnboardingResult> StartStripeConnectAsync(string returnUrl, string refreshUrl, CancellationToken ct = default);
    Task SyncStripeConnectAsync(CancellationToken ct = default);
    Task<TutorPayPalOAuthStart> StartPayPalOAuthAsync(string returnUrl, CancellationToken ct = default);
    Task CompletePayPalOAuthAsync(string? maskedEmail, CancellationToken ct = default);
}

public class TutorPayoutAccountService : ITutorPayoutAccountService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITutorDisbursementGateway _gateway;

    public TutorPayoutAccountService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITutorDisbursementGateway gateway)
    {
        _db = db;
        _tenantContext = tenantContext;
        _gateway = gateway;
    }

    public Task<TutorPayoutSetupDto> GetSetupAsync(CancellationToken ct = default)
    {
        var tenant = RequireTenant();
        return Task.FromResult(BuildSetup(tenant));
    }

    public async Task<TutorPayoutSetupDto> UpdateProfileAsync(
        UpdateTutorPayoutProfileRequest request,
        CancellationToken ct = default)
    {
        var tenant = RequireTenant();

        if (!string.IsNullOrWhiteSpace(request.Country))
            tenant.Country = TutorPayoutPolicy.NormalizeCountry(request.Country);

        if (request.PayPalEmail is not null)
        {
            var email = request.PayPalEmail.Trim();
            if (email.Length > 0 && !email.Contains('@'))
                throw new InvalidOperationException("Adresse e-mail PayPal invalide.");
            tenant.PayPalEmail = string.IsNullOrWhiteSpace(email) ? null : email.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(tenant.PayPalEmail))
                await EnsurePayPalAccountAsync(tenant, tenant.PayPalEmail, ct);
        }

        // StripeAccountId n'est plus saisi manuellement (onboarding Connect via PayGateway).
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return BuildSetup(tenant);
    }

    public async Task<TutorPayoutAccountDto> UpsertAccountAsync(
        UpsertTutorPayoutAccountRequest request,
        Guid? id = null,
        CancellationToken ct = default)
    {
        var tenant = RequireTenant();
        if (!Enum.TryParse<PayoutProviderKind>(request.ProviderKind, ignoreCase: true, out var kind))
            throw new InvalidOperationException("Type de moyen de paiement inconnu.");

        ValidateAccountPayload(kind, request);

        string? externalToken = request.EmailOrAccountId;
        string? maskedPhone = null;

        if (PayoutProviderCodes.IsMobileMoney(kind))
        {
            if (_gateway.IsConfigured)
            {
                var validation = await _gateway.ValidateMobileMoneyAsync(
                    TutorPayoutPolicy.NormalizeCountry(request.CountryCode ?? tenant.Country),
                    PayoutProviderCodes.ToPayGatewayCode(kind),
                    request.PhoneNumber!,
                    request.AccountHolderName,
                    ct);
                if (!validation.IsValid)
                    throw new InvalidOperationException(validation.Message ?? "Numéro Mobile Money invalide.");

                externalToken = validation.ExternalToken;
                maskedPhone = validation.MaskedPhone;

                var externalRef = $"tutor-{tenant.Id:N}-{kind}";
                await _gateway.RegisterMobileMoneyRecipientAsync(
                    externalRef,
                    TutorPayoutPolicy.NormalizeCountry(request.CountryCode ?? tenant.Country),
                    PayoutProviderCodes.ToPayGatewayCode(kind),
                    request.PhoneNumber!,
                    request.AccountHolderName,
                    ct);
            }
        }

        TutorPayoutAccount account;
        if (id is Guid existingId)
        {
            account = _db.TutorPayoutAccounts.FirstOrDefault(a => a.Id == existingId)
                ?? throw new InvalidOperationException("Compte de versement introuvable.");
        }
        else
        {
            account = new TutorPayoutAccount { TenantId = tenant.Id };
            _db.Add(account);
        }

        account.Label = request.Label.Trim();
        account.ProviderKind = kind;
        account.CountryCode = TutorPayoutPolicy.NormalizeCountry(request.CountryCode ?? tenant.Country);
        account.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? TutorPayoutPolicy.PolicyCurrency
            : request.Currency.Trim().ToUpperInvariant();
        account.AccountHolderName = request.AccountHolderName.Trim();
        account.EmailOrAccountId = string.IsNullOrWhiteSpace(externalToken)
            ? (string.IsNullOrWhiteSpace(request.EmailOrAccountId) ? null : request.EmailOrAccountId.Trim())
            : externalToken.Trim();
        account.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        account.PaymentDetails = string.IsNullOrWhiteSpace(request.PaymentDetails)
            ? maskedPhone
            : request.PaymentDetails.Trim();
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        if (PayoutProviderCodes.IsMobileMoney(kind))
        {
            account.IsVerified = true;
            account.VerifiedAt = DateTime.UtcNow;
        }

        if (request.IsPrimary || !_db.TutorPayoutAccounts.Any(a => a.TenantId == tenant.Id && a.IsPrimary && a.Id != account.Id))
        {
            foreach (var other in _db.TutorPayoutAccounts.Where(a => a.TenantId == tenant.Id && a.IsPrimary))
                other.IsPrimary = false;
            account.IsPrimary = true;
        }
        else
        {
            account.IsPrimary = false;
        }

        if (kind == PayoutProviderKind.PayPal && !string.IsNullOrWhiteSpace(account.EmailOrAccountId))
            tenant.PayPalEmail = account.EmailOrAccountId;
        if (kind == PayoutProviderKind.StripeConnect && !string.IsNullOrWhiteSpace(account.EmailOrAccountId))
            tenant.StripeAccountId = account.EmailOrAccountId;

        await _db.SaveChangesAsync(ct);
        return MapAccount(account);
    }

    public async Task SetPrimaryAsync(Guid accountId, CancellationToken ct = default)
    {
        RequireTenantId();
        var account = _db.TutorPayoutAccounts.FirstOrDefault(a => a.Id == accountId && a.IsActive)
            ?? throw new InvalidOperationException("Compte de versement introuvable.");

        foreach (var other in _db.TutorPayoutAccounts.Where(a => a.IsPrimary))
            other.IsPrimary = false;
        account.IsPrimary = true;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid accountId, CancellationToken ct = default)
    {
        RequireTenantId();
        var account = _db.TutorPayoutAccounts.FirstOrDefault(a => a.Id == accountId)
            ?? throw new InvalidOperationException("Compte de versement introuvable.");
        account.IsActive = false;
        account.IsPrimary = false;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TutorConnectOnboardingResult> StartStripeConnectAsync(
        string returnUrl, string refreshUrl, CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured)
            throw new InvalidOperationException("PayGateway n'est pas configuré pour Stripe Connect.");

        var tenant = RequireTenant();
        var reference = $"tutor-{tenant.Id:N}";
        var result = await _gateway.StartStripeConnectOnboardingAsync(
            reference,
            TutorPayoutPolicy.NormalizeCountry(tenant.Country),
            TutorPayoutPolicy.PolicyCurrency,
            tenant.PayPalEmail,
            returnUrl,
            refreshUrl,
            ct);

        var account = _db.TutorPayoutAccounts.FirstOrDefault(a =>
            a.ProviderKind == PayoutProviderKind.StripeConnect && a.IsActive);
        if (account is null)
        {
            account = new TutorPayoutAccount
            {
                TenantId = tenant.Id,
                Label = "Stripe Connect",
                ProviderKind = PayoutProviderKind.StripeConnect,
                CountryCode = TutorPayoutPolicy.NormalizeCountry(tenant.Country),
                Currency = TutorPayoutPolicy.PolicyCurrency,
                AccountHolderName = tenant.Name,
                IsPrimary = !_db.TutorPayoutAccounts.Any(a => a.IsPrimary && a.IsActive)
            };
            _db.Add(account);
        }

        account.EmailOrAccountId = result.ExternalAccountId;
        account.UpdatedAt = DateTime.UtcNow;
        tenant.StripeAccountId = result.ExternalAccountId;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task SyncStripeConnectAsync(CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured) return;
        var tenant = RequireTenant();
        var account = _db.TutorPayoutAccounts.FirstOrDefault(a =>
            a.ProviderKind == PayoutProviderKind.StripeConnect && a.IsActive
            && !string.IsNullOrWhiteSpace(a.EmailOrAccountId));
        if (account?.EmailOrAccountId is null) return;

        var status = await _gateway.GetStripeConnectAccountAsync(account.EmailOrAccountId, ct);
        if (status is null) return;

        account.IsVerified = status.PayoutsEnabled && status.DetailsSubmitted;
        account.VerifiedAt = account.IsVerified ? DateTime.UtcNow : account.VerifiedAt;
        account.PaymentDetails = status.MaskedEmail;
        account.UpdatedAt = DateTime.UtcNow;
        tenant.StripeAccountId = status.ExternalAccountId;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TutorPayPalOAuthStart> StartPayPalOAuthAsync(string returnUrl, CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured)
            throw new InvalidOperationException("PayGateway / PayPal OAuth non configuré. Utilisez l'e-mail PayPal.");

        var tenant = RequireTenant();
        var reference = $"tutor-{tenant.Id:N}";
        var result = await _gateway.StartPayPalOAuthAsync(reference, returnUrl, ct);
        return result ?? throw new InvalidOperationException("Impossible de démarrer OAuth PayPal.");
    }

    public async Task CompletePayPalOAuthAsync(string? maskedEmail, CancellationToken ct = default)
    {
        var tenant = RequireTenant();
        var reference = $"tutor-{tenant.Id:N}";
        string? masked = maskedEmail;

        if (_gateway.IsConfigured)
        {
            var linked = await _gateway.GetPayPalAccountAsync(reference, ct);
            if (linked is not null)
                masked = linked.MaskedEmail ?? masked;
        }

        masked ??= "PayPal ••••";
        var account = _db.TutorPayoutAccounts.FirstOrDefault(a =>
            a.ProviderKind == PayoutProviderKind.PayPal && a.IsActive
            && a.EmailOrAccountId == reference);
        if (account is null)
        {
            account = new TutorPayoutAccount
            {
                TenantId = tenant.Id,
                Label = "PayPal",
                ProviderKind = PayoutProviderKind.PayPal,
                CountryCode = TutorPayoutPolicy.NormalizeCountry(tenant.Country),
                Currency = TutorPayoutPolicy.PolicyCurrency,
                AccountHolderName = tenant.Name,
                EmailOrAccountId = reference,
                IsPrimary = !_db.TutorPayoutAccounts.Any(a => a.IsPrimary && a.IsActive)
            };
            _db.Add(account);
        }

        account.PaymentDetails = masked;
        account.IsVerified = true;
        account.VerifiedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsurePayPalAccountAsync(Tenant tenant, string email, CancellationToken ct)
    {
        var existing = _db.TutorPayoutAccounts.FirstOrDefault(a =>
            a.ProviderKind == PayoutProviderKind.PayPal && a.IsActive
            && a.EmailOrAccountId == email);
        if (existing is not null) return;

        _db.Add(new TutorPayoutAccount
        {
            TenantId = tenant.Id,
            Label = "PayPal",
            ProviderKind = PayoutProviderKind.PayPal,
            CountryCode = TutorPayoutPolicy.NormalizeCountry(tenant.Country),
            Currency = TutorPayoutPolicy.PolicyCurrency,
            AccountHolderName = tenant.Name,
            EmailOrAccountId = email,
            IsPrimary = !_db.TutorPayoutAccounts.Any(a => a.IsPrimary && a.IsActive),
            IsActive = true
        });
        await Task.CompletedTask;
    }

    private TutorPayoutSetupDto BuildSetup(Tenant tenant)
    {
        var country = TutorPayoutPolicy.NormalizeCountry(tenant.Country);
        var region = TutorPayoutPolicy.ResolveRegion(country);
        var required = TutorPayoutPolicy.RequiredProviders(region);
        var accounts = _db.TutorPayoutAccounts
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.Label)
            .ToList()
            .Select(MapAccount)
            .ToList();

        var stripeOk = !string.IsNullOrWhiteSpace(tenant.StripeAccountId)
            || accounts.Any(a => a.ProviderKind.Equals(nameof(PayoutProviderKind.StripeConnect), StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrWhiteSpace(a.EmailOrAccountId));
        var paypalOk = !string.IsNullOrWhiteSpace(tenant.PayPalEmail)
            || accounts.Any(a => a.ProviderKind.Equals(nameof(PayoutProviderKind.PayPal), StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrWhiteSpace(a.EmailOrAccountId));

        var mobileMoneyOk = accounts.Any(a =>
            Enum.TryParse<PayoutProviderKind>(a.ProviderKind, true, out var kind)
            && PayoutProviderCodes.IsMobileMoney(kind)
            && !string.IsNullOrWhiteSpace(a.PhoneNumber)
            && !string.IsNullOrWhiteSpace(a.AccountHolderName));

        var setupComplete = region switch
        {
            PayoutRegionKind.StripeConnectZone => stripeOk && paypalOk,
            PayoutRegionKind.Africa => mobileMoneyOk,
            _ => paypalOk
        };

        var catalog = new List<PayoutProviderCatalogItemDto>();
        if (region == PayoutRegionKind.Africa)
        {
            foreach (var p in TutorPayoutPolicy.AfricaMobileMoneyProviders)
            {
                catalog.Add(new PayoutProviderCatalogItemDto(
                    p.ToString(), DisplayName(p), Required: false, region.ToString()));
            }
        }
        else
        {
            foreach (var p in required)
                catalog.Add(new PayoutProviderCatalogItemDto(p.ToString(), DisplayName(p), true, region.ToString()));
            foreach (var extra in Enum.GetValues<PayoutProviderKind>())
            {
                if (required.Contains(extra)) continue;
                if (PayoutProviderCodes.IsMobileMoney(extra)) continue;
                catalog.Add(new PayoutProviderCatalogItemDto(extra.ToString(), DisplayName(extra), false, region.ToString()));
            }
        }

        return new TutorPayoutSetupDto(
            region.ToString(),
            country,
            TutorPayoutPolicy.PolicyCurrency,
            required.Select(r => r.ToString()).ToList(),
            stripeOk,
            paypalOk,
            setupComplete,
            tenant.StripeAccountId,
            tenant.PayPalEmail,
            accounts,
            catalog);
    }

    private static void ValidateAccountPayload(PayoutProviderKind kind, UpsertTutorPayoutAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new InvalidOperationException("Libellé obligatoire.");
        if (string.IsNullOrWhiteSpace(request.AccountHolderName))
            throw new InvalidOperationException("Nom du titulaire obligatoire (info publique).");

        switch (kind)
        {
            case PayoutProviderKind.PayPal:
                if (string.IsNullOrWhiteSpace(request.EmailOrAccountId) || !request.EmailOrAccountId.Contains('@'))
                    throw new InvalidOperationException("E-mail PayPal obligatoire.");
                break;
            case PayoutProviderKind.StripeConnect:
                throw new InvalidOperationException("Utilisez l'onboarding Stripe Connect (pas de saisie manuelle acct_).");
            default:
                if (PayoutProviderCodes.IsMobileMoney(kind))
                {
                    if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                        throw new InvalidOperationException("Numéro de téléphone public obligatoire (aucun PIN / OTP).");
                }
                break;
        }
    }

    private static string DisplayName(PayoutProviderKind kind) => kind switch
    {
        PayoutProviderKind.StripeConnect => "Stripe Connect",
        PayoutProviderKind.PayPal => "PayPal",
        PayoutProviderKind.Wave => "Wave",
        PayoutProviderKind.TapTapSend => "TapTap Send",
        PayoutProviderKind.OrangeMoney => "Orange Money",
        PayoutProviderKind.MtnMomo => "MTN MoMo",
        PayoutProviderKind.Mpesa => "M-Pesa",
        PayoutProviderKind.Moov => "Moov Money",
        PayoutProviderKind.Airtel => "Airtel Money",
        _ => kind.ToString()
    };

    private static TutorPayoutAccountDto MapAccount(TutorPayoutAccount a) => new(
        a.Id,
        a.Label,
        a.ProviderKind.ToString(),
        a.CountryCode,
        a.Currency,
        a.IsPrimary,
        a.IsActive,
        a.AccountHolderName,
        // Ne jamais exposer acct_ brut : masquer côté DTO pour Stripe
        a.ProviderKind == PayoutProviderKind.StripeConnect
            ? (string.IsNullOrWhiteSpace(a.EmailOrAccountId) ? null : "Stripe Connect configuré")
            : a.EmailOrAccountId,
        a.PhoneNumber,
        a.PaymentDetails,
        a.IsVerified,
        a.VerifiedAt);

    private Guid RequireTenantId() =>
        _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant requis.");

    private Tenant RequireTenant()
    {
        var id = RequireTenantId();
        return _db.Tenants.FirstOrDefault(t => t.Id == id)
            ?? throw new InvalidOperationException("École introuvable.");
    }
}
