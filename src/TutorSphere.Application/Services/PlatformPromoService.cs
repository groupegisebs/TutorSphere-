using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.PlatformBilling;
using TutorSphere.Application.DTOs.PlatformPromo;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IPlatformPromoService
{
    Task<IReadOnlyList<PlatformPromoCodeDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PlatformPromoCodeDto>> CreateAsync(CreatePlatformPromoCodeRequest request, CancellationToken ct = default);
    Task<PlatformPromoCodeDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<PlatformLicensePaymentStatusDto> RedeemForOwnerAsync(
        string ownerUserId,
        string code,
        bool autoRenewAtSource = false,
        CancellationToken ct = default);
    Task<PlatformLicensePaymentStatusDto> RedeemForTenantAsync(
        Guid tenantId,
        string code,
        string actorUserId,
        bool skipRenewalWindow = false,
        bool createIfMissing = false,
        bool autoRenewAtSource = false,
        CancellationToken ct = default);
}

public sealed class PlatformPromoService(IApplicationDbContext db) : IPlatformPromoService
{
    public Task<IReadOnlyList<PlatformPromoCodeDto>> ListAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var codes = db.PlatformPromoCodes
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var tenantIds = codes
            .Where(c => c.RedeemedByTenantId.HasValue)
            .Select(c => c.RedeemedByTenantId!.Value)
            .Distinct()
            .ToList();

        var schoolNames = db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToList()
            .ToDictionary(t => t.Id, t => t.Name);

        IReadOnlyList<PlatformPromoCodeDto> result = codes
            .Select(c => Map(c, now, c.RedeemedByTenantId is Guid tid && schoolNames.TryGetValue(tid, out var n) ? n : null))
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<IReadOnlyList<PlatformPromoCodeDto>> CreateAsync(
        CreatePlatformPromoCodeRequest request,
        CancellationToken ct = default)
    {
        var quantity = Math.Clamp(request.Quantity, 1, 100);
        var years = Math.Clamp(request.LicenseYears <= 0 ? 1 : request.LicenseYears, 1, 5);
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var expiresAt = NormalizeExpiry(request.ExpiresAt);

        var created = new List<PlatformPromoCode>();

        if (quantity == 1 && !string.IsNullOrWhiteSpace(request.Code))
        {
            var code = ActivationKeyFormat.Normalize(request.Code);
            ActivationKeyFormat.EnsureFormat(code);
            if (db.PlatformPromoCodes.Any(c => c.Code == code))
                throw new InvalidOperationException("Cette clé d'activation existe déjà.");

            var entity = new PlatformPromoCode
            {
                Code = code,
                LicenseYears = years,
                ExpiresAt = expiresAt,
                Notes = notes,
                IsActive = true
            };
            db.Add(entity);
            created.Add(entity);
        }
        else
        {
            for (var i = 0; i < quantity; i++)
            {
                string code;
                do
                {
                    code = ActivationKeyFormat.Generate();
                } while (db.PlatformPromoCodes.Any(c => c.Code == code) || created.Any(c => c.Code == code));

                var entity = new PlatformPromoCode
                {
                    Code = code,
                    LicenseYears = years,
                    ExpiresAt = expiresAt,
                    Notes = notes,
                    IsActive = true
                };
                db.Add(entity);
                created.Add(entity);
            }
        }

        await db.SaveChangesAsync(ct);
        var now = DateTime.UtcNow;
        return created.Select(c => Map(c, now, null)).ToList();
    }

    public async Task<PlatformPromoCodeDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = db.PlatformPromoCodes.FirstOrDefault(c => c.Id == id)
            ?? throw new InvalidOperationException("Clé d'activation introuvable.");

        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;

        // Désactiver un code déjà utilisé → révoquer la licence gratuite : l'enseignant doit payer.
        if (!isActive && entity.RedeemedByTenantId is Guid redeemedTenantId)
            await RevokePromoLicenseAsync(entity, redeemedTenantId, ct);

        await db.SaveChangesAsync(ct);

        string? schoolName = null;
        if (entity.RedeemedByTenantId is Guid tid)
            schoolName = db.Tenants.Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefault();

        return Map(entity, DateTime.UtcNow, schoolName);
    }

    /// <summary>
    /// Annule le paiement PROMO:{code} et recalcule LicenseExpiresAt.
    /// S'il ne reste aucun paiement Completed, l'école passe en AwaitingRenewal (doit payer).
    /// </summary>
    private async Task RevokePromoLicenseAsync(
        PlatformPromoCode promo,
        Guid tenantId,
        CancellationToken ct)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        if (tenant is null)
            return;

        var promoMarker = $"PROMO:{promo.Code}";
        var promoPayments = db.PlatformLicensePaymentsForAnyTenant
            .Where(p => p.TenantId == tenantId
                        && p.GatewayPaymentCode == promoMarker
                        && p.Status == PaymentStatus.Completed)
            .ToList();

        foreach (var payment in promoPayments)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        var completedEnds = db.PlatformLicensePaymentsForAnyTenant
            .Where(p => p.TenantId == tenantId
                        && p.Status == PaymentStatus.Completed
                        && p.PeriodEnd != null)
            .Select(p => p.PeriodEnd!.Value)
            .ToList();

        if (completedEnds.Count == 0)
        {
            tenant.LicenseExpiresAt = DateTime.UtcNow;
            tenant.IsPublicProfile = false;
            if (tenant.Status is TenantStatus.Active or TenantStatus.AwaitingOnboarding)
                tenant.Status = TenantStatus.AwaitingRenewal;
        }
        else
        {
            tenant.LicenseExpiresAt = completedEnds.Max();
            if (tenant.LicenseExpiresAt <= DateTime.UtcNow)
            {
                tenant.IsPublicProfile = false;
                if (tenant.Status is TenantStatus.Active or TenantStatus.AwaitingOnboarding)
                    tenant.Status = TenantStatus.AwaitingRenewal;
            }
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await Task.CompletedTask;
    }

    public async Task<PlatformLicensePaymentStatusDto> RedeemForOwnerAsync(
        string ownerUserId,
        string code,
        bool autoRenewAtSource = false,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
            ?? throw new InvalidOperationException("Aucun profil enseignant associé à ce compte.");

        return await RedeemForTenantAsync(
            tenant.Id,
            code,
            ownerUserId,
            skipRenewalWindow: false,
            createIfMissing: false,
            autoRenewAtSource: autoRenewAtSource,
            ct);
    }

    public async Task<PlatformLicensePaymentStatusDto> RedeemForTenantAsync(
        Guid tenantId,
        string code,
        string actorUserId,
        bool skipRenewalWindow,
        bool createIfMissing,
        bool autoRenewAtSource = false,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Aucun profil enseignant associé à ce compte.");

        if (tenant.HasPaidLicense() && tenant.RequiresOnboarding() && !skipRenewalWindow)
        {
            throw new InvalidOperationException(
                "La session est déjà active. L'enseignant doit compléter l'auto-formation.");
        }

        if (!skipRenewalWindow
            && tenant.HasPaidLicense()
            && tenant.OnboardingCompletedAt is not null
            && tenant.LicenseExpiresAt is { } expires
            && expires > DateTime.UtcNow.AddDays(30))
        {
            throw new InvalidOperationException(
                "La session enseignant est déjà active. Le renouvellement sera disponible 1 mois avant l'échéance.");
        }

        var normalized = ActivationKeyFormat.Normalize(code);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Clé d'activation invalide.");

        var promo = db.PlatformPromoCodes.FirstOrDefault(c => c.Code == normalized);
        if (promo is null)
        {
            if (!createIfMissing)
                throw new InvalidOperationException("Clé d'activation invalide ou déjà utilisée.");
            ActivationKeyFormat.EnsureFormat(normalized);
            promo = new PlatformPromoCode
            {
                Code = normalized,
                LicenseYears = 1,
                IsActive = true,
                Notes = "Créé à l'activation de session enseignant."
            };
            db.Add(promo);
        }

        if (!promo.IsAvailable())
            throw new InvalidOperationException("Clé d'activation invalide, expirée ou déjà utilisée.");

        var now = DateTime.UtcNow;
        LicenseFeeWithholding.GrantLicenseYears(tenant, promo.LicenseYears, now);
        tenant.LicenseFeeWithholdingRemainingUsd = 0;
        tenant.LicenseSettlementKind = LicenseFeeWithholding.SettlementPromo;
        tenant.LicenseAutoRenewAtSource = autoRenewAtSource;

        promo.RedeemedAt = now;
        promo.RedeemedByTenantId = tenant.Id;
        promo.RedeemedByUserId = actorUserId;
        promo.UpdatedAt = now;

        var payment = new PlatformLicensePayment
        {
            TenantId = tenant.Id,
            Amount = 0m,
            Currency = "USD",
            Status = PaymentStatus.Completed,
            GatewayPaymentCode = $"PROMO:{promo.Code}",
            PeriodStart = now,
            PeriodEnd = tenant.LicenseExpiresAt,
            CompletedAt = now
        };
        db.Add(payment);

        await db.SaveChangesAsync(ct);

        return new PlatformLicensePaymentStatusDto(
            payment.Id,
            payment.GatewayPaymentCode ?? promo.Code,
            "Completed",
            payment.Status.ToString(),
            payment.CompletedAt,
            tenant.LicenseExpiresAt,
            tenant.HasValidLicense(),
            tenant.HasPaidLicense(),
            tenant.RequiresOnboarding());
    }

    private static PlatformPromoCodeDto Map(PlatformPromoCode c, DateTime now, string? schoolName) =>
        new(
            c.Id,
            c.Code,
            c.IsActive,
            c.LicenseYears,
            c.ExpiresAt,
            c.Notes,
            c.CreatedAt,
            c.RedeemedAt,
            c.RedeemedByTenantId,
            c.RedeemedByUserId,
            schoolName,
            c.IsAvailable(now));

    private static DateTime? NormalizeExpiry(DateTime? expiresAt)
    {
        if (expiresAt is null) return null;
        var utc = expiresAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)
            : expiresAt.Value.ToUniversalTime();
        return utc.Date.AddDays(1).AddTicks(-1);
    }
}
