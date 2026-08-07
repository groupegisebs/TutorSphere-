using System.Security.Cryptography;
using System.Text;
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
    Task<PlatformLicensePaymentStatusDto> RedeemForOwnerAsync(string ownerUserId, string code, CancellationToken ct = default);
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
            var code = NormalizeCode(request.Code);
            EnsureCodeFormat(code);
            if (db.PlatformPromoCodes.Any(c => c.Code == code))
                throw new InvalidOperationException("Ce code promo existe déjà.");

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
                    code = GenerateCode();
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
            ?? throw new InvalidOperationException("Code promo introuvable.");

        entity.IsActive = isActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        string? schoolName = null;
        if (entity.RedeemedByTenantId is Guid tid)
            schoolName = db.Tenants.Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefault();

        return Map(entity, DateTime.UtcNow, schoolName);
    }

    public async Task<PlatformLicensePaymentStatusDto> RedeemForOwnerAsync(
        string ownerUserId,
        string code,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
            ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");

        if (tenant.HasPaidLicense() && tenant.RequiresOnboarding())
        {
            throw new InvalidOperationException(
                "Votre licence est déjà active. Complétez l'auto-formation pour ouvrir votre établissement.");
        }

        if (tenant.HasPaidLicense()
            && tenant.OnboardingCompletedAt is not null
            && tenant.LicenseExpiresAt is { } expires
            && expires > DateTime.UtcNow.AddDays(30))
        {
            throw new InvalidOperationException(
                "Votre établissement est déjà actif. Le code promo n'est pas nécessaire pour le moment.");
        }

        var normalized = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Code promo invalide.");

        var promo = db.PlatformPromoCodes.FirstOrDefault(c => c.Code == normalized)
            ?? throw new InvalidOperationException("Code promo invalide ou déjà utilisé.");

        if (!promo.IsAvailable())
            throw new InvalidOperationException("Code promo invalide, expiré ou déjà utilisé.");

        var periodStart = DateTime.UtcNow;
        if (tenant.LicenseExpiresAt is { } current && current > periodStart)
            periodStart = current;

        var periodEnd = periodStart.AddYears(promo.LicenseYears);

        promo.RedeemedAt = DateTime.UtcNow;
        promo.RedeemedByTenantId = tenant.Id;
        promo.RedeemedByUserId = ownerUserId;
        promo.UpdatedAt = DateTime.UtcNow;

        tenant.LicenseExpiresAt = periodEnd;
        tenant.UpdatedAt = DateTime.UtcNow;
        if (tenant.OnboardingCompletedAt is null)
        {
            tenant.Status = TenantStatus.AwaitingOnboarding;
            tenant.IsPublicProfile = false;
        }
        else
        {
            tenant.Status = TenantStatus.Active;
            tenant.IsPublicProfile = true;
        }

        var payment = new PlatformLicensePayment
        {
            TenantId = tenant.Id,
            Amount = 0m,
            Currency = "USD",
            Status = PaymentStatus.Completed,
            GatewayPaymentCode = $"PROMO:{promo.Code}",
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CompletedAt = DateTime.UtcNow
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

    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

    private static void EnsureCodeFormat(string code)
    {
        if (code.Length is < 4 or > 32)
            throw new InvalidOperationException("Le code doit contenir entre 4 et 32 caractères.");

        if (!code.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'))
            throw new InvalidOperationException("Le code ne peut contenir que des lettres, chiffres, - ou _.");
    }

    private static DateTime? NormalizeExpiry(DateTime? expiresAt)
    {
        if (expiresAt is null) return null;
        var utc = expiresAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)
            : expiresAt.Value.ToUniversalTime();
        return utc.Date.AddDays(1).AddTicks(-1);
    }

    private static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(12);
        sb.Append("TS-");
        for (var i = 0; i < 8; i++)
            sb.Append(alphabet[bytes[i] % alphabet.Length]);
        return sb.ToString();
    }
}
