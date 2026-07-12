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
}

public class TutorPayoutAccountService : ITutorPayoutAccountService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TutorPayoutAccountService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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
        }

        if (request.StripeAccountId is not null)
        {
            var acct = request.StripeAccountId.Trim();
            tenant.StripeAccountId = string.IsNullOrWhiteSpace(acct) ? null : acct;
        }

        // Exigences inscription / configuration
        var region = TutorPayoutPolicy.ResolveRegion(tenant.Country);
        if (TutorPayoutPolicy.RequiresPayPalAtSignup(tenant.Country)
            && string.IsNullOrWhiteSpace(tenant.PayPalEmail))
            throw new InvalidOperationException(
                "Un compte PayPal (e-mail) est obligatoire pour recevoir vos paiements dans votre zone.");

        if (region == PayoutRegionKind.StripeConnectZone
            && string.IsNullOrWhiteSpace(tenant.StripeAccountId))
        {
            // Autoriser la sauvegarde partielle (pays + PayPal) ; le setup restera incomplet.
        }

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
        account.EmailOrAccountId = string.IsNullOrWhiteSpace(request.EmailOrAccountId)
            ? null
            : request.EmailOrAccountId.Trim();
        account.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        account.PaymentDetails = string.IsNullOrWhiteSpace(request.PaymentDetails)
            ? null
            : request.PaymentDetails.Trim();
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

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

        // Miroir sur Tenant pour Stripe / PayPal
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

        var waveOk = accounts.Any(a => a.ProviderKind.Equals(nameof(PayoutProviderKind.Wave), StringComparison.OrdinalIgnoreCase)
                                       && !string.IsNullOrWhiteSpace(a.PhoneNumber));
        var ttsOk = accounts.Any(a => a.ProviderKind.Equals(nameof(PayoutProviderKind.TapTapSend), StringComparison.OrdinalIgnoreCase)
                                      && (!string.IsNullOrWhiteSpace(a.PhoneNumber) || !string.IsNullOrWhiteSpace(a.EmailOrAccountId)));

        var setupComplete = region switch
        {
            PayoutRegionKind.StripeConnectZone => stripeOk && paypalOk,
            PayoutRegionKind.Africa => waveOk && ttsOk,
            _ => paypalOk
        };

        var catalog = required.Select(p => new PayoutProviderCatalogItemDto(
            p.ToString(),
            DisplayName(p),
            Required: true,
            region.ToString())).ToList();

        // Ajouter les optionnels du catalogue pour la région
        foreach (var extra in Enum.GetValues<PayoutProviderKind>())
        {
            if (required.Contains(extra)) continue;
            if (region == PayoutRegionKind.StripeConnectZone && extra is PayoutProviderKind.Wave or PayoutProviderKind.TapTapSend)
                continue;
            if (region == PayoutRegionKind.Africa && extra == PayoutProviderKind.StripeConnect)
                continue;
            catalog.Add(new PayoutProviderCatalogItemDto(extra.ToString(), DisplayName(extra), false, region.ToString()));
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
            throw new InvalidOperationException("Nom du titulaire obligatoire.");

        switch (kind)
        {
            case PayoutProviderKind.PayPal:
                if (string.IsNullOrWhiteSpace(request.EmailOrAccountId) || !request.EmailOrAccountId.Contains('@'))
                    throw new InvalidOperationException("E-mail PayPal obligatoire.");
                break;
            case PayoutProviderKind.StripeConnect:
                if (string.IsNullOrWhiteSpace(request.EmailOrAccountId))
                    throw new InvalidOperationException("Identifiant de compte Stripe Connect (acct_…) obligatoire.");
                break;
            case PayoutProviderKind.Wave:
            case PayoutProviderKind.TapTapSend:
                if (string.IsNullOrWhiteSpace(request.PhoneNumber) && string.IsNullOrWhiteSpace(request.EmailOrAccountId))
                    throw new InvalidOperationException("Numéro de téléphone (ou identifiant) obligatoire pour Wave / TapTap Send.");
                break;
        }
    }

    private static string DisplayName(PayoutProviderKind kind) => kind switch
    {
        PayoutProviderKind.StripeConnect => "Stripe Connect",
        PayoutProviderKind.PayPal => "PayPal",
        PayoutProviderKind.Wave => "Wave",
        PayoutProviderKind.TapTapSend => "TapTap Send",
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
        a.EmailOrAccountId,
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
