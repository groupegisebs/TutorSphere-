using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Common;

/// <summary>
/// Licence annuelle enseignant : 10 $ USD, ou code promo, ou retenue à la source
/// sur les premiers montants dus à l'enseignant (équivalent 10 $ USD).
/// </summary>
public static class LicenseFeeWithholding
{
    public const decimal AnnualFeeUsd = 10.00m;
    public const string SettlementPay = "pay";
    public const string SettlementPromo = "promo";
    public const string SettlementWithhold = "withhold";

    /// <summary>Taux approximatifs pour convertir 10 $ USD dans la devise du paiement parent.</summary>
    public const decimal UsdPerCad = 0.72m;
    public const decimal UsdPerEur = 1.08m;
    public const decimal UsdPerGbp = 1.27m;
    public const decimal UsdPerXaf = 1m / 600m;

    public static bool IsKnownSettlement(string? value) =>
        string.Equals(value, SettlementPay, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, SettlementPromo, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, SettlementWithhold, StringComparison.OrdinalIgnoreCase);

    public static decimal ToUsd(decimal amount, string? currency)
    {
        var code = (currency ?? "USD").Trim().ToUpperInvariant();
        var rate = code switch
        {
            "USD" => 1m,
            "CAD" => UsdPerCad,
            "EUR" => UsdPerEur,
            "GBP" => UsdPerGbp,
            "XAF" or "XOF" => UsdPerXaf,
            _ => 1m
        };
        return decimal.Round(amount * rate, 4, MidpointRounding.AwayFromZero);
    }

    public static decimal FromUsd(decimal usd, string? currency)
    {
        var code = (currency ?? "USD").Trim().ToUpperInvariant();
        var local = code switch
        {
            "USD" => usd,
            "CAD" => usd / UsdPerCad,
            "EUR" => usd / UsdPerEur,
            "GBP" => usd / UsdPerGbp,
            "XAF" or "XOF" => usd / UsdPerXaf,
            _ => usd
        };
        return decimal.Round(local, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Prélève l'équivalent USD restant sur la part enseignant d'un paiement parent.
    /// La retenue rejoint les frais plateforme. Idempotent si le solde restant est à 0.
    /// </summary>
    public static decimal TakeFromTutorShare(Tenant tenant, Payment payment)
    {
        var remainingUsd = tenant.LicenseFeeWithholdingRemainingUsd;
        if (remainingUsd <= 0 || payment.TutorAmount <= 0)
            return 0m;

        var tutorUsd = ToUsd(payment.TutorAmount, payment.Currency);
        if (tutorUsd <= 0)
            return 0m;

        var takeUsd = Math.Min(remainingUsd, tutorUsd);
        var takeLocal = FromUsd(takeUsd, payment.Currency);
        takeLocal = Math.Min(takeLocal, payment.TutorAmount);
        if (takeLocal <= 0)
            return 0m;

        payment.TutorAmount -= takeLocal;
        payment.PlatformFee += takeLocal;
        tenant.LicenseFeeWithholdingRemainingUsd = Math.Max(
            0m,
            decimal.Round(remainingUsd - ToUsd(takeLocal, payment.Currency), 2, MidpointRounding.AwayFromZero));
        tenant.UpdatedAt = DateTime.UtcNow;
        return takeLocal;
    }

    public static void GrantLicenseYears(Tenant tenant, int years, DateTime utcNow)
    {
        years = Math.Clamp(years <= 0 ? 1 : years, 1, 5);
        var periodStart = utcNow;
        if (tenant.LicenseExpiresAt is { } current && current > periodStart)
            periodStart = current;

        tenant.LicenseExpiresAt = periodStart.AddYears(years);
        tenant.LicenseRenewalReminderSentAt = null;
        tenant.UpdatedAt = utcNow;

        if (tenant.OnboardingCompletedAt is null)
        {
            tenant.Status = TenantStatus.AwaitingOnboarding;
            tenant.IsPublicProfile = false;
        }
        else
        {
            tenant.Status = TenantStatus.Active;
            if (tenant.ExpertApprovalStatus == ExpertApprovalStatus.Approved)
                tenant.IsPublicProfile = true;
        }
    }
}
