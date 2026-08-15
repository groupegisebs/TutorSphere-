using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.PlatformBilling;
using TutorSphere.Application.DTOs.PlatformPromo;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public record ActivateTeacherSessionRequest(
    string Settlement,
    string? PromoCode = null,
    bool AutoRenewAtSource = false);

public interface ITeacherLicenseActivationService
{
    /// <summary>
    /// Active ou prolonge la session enseignant (licence annuelle) : code promo ou retenue.
    /// Réservé au super-admin / admin plateforme, ou à l'admin du groupe, à tout moment.
    /// </summary>
    Task<PlatformLicensePaymentStatusDto> ActivateSessionAsync(
        Guid tenantId,
        string actorUserId,
        ActivateTeacherSessionRequest request,
        bool asPlatformAdmin,
        CancellationToken ct = default);

    /// <summary>Applique paiement / promo / retenue au moment de la validation de candidature.</summary>
    Task ApplySettlementOnApprovalAsync(
        Guid tenantId,
        string actorUserId,
        string? settlement,
        string? promoCode,
        bool asPlatformAdmin,
        bool autoRenewAtSource = false,
        CancellationToken ct = default);

    /// <summary>Annule la retenue restante (licence payée ou code promo).</summary>
    void ClearWithholding(Tenant tenant);

    /// <summary>Retenue à la source sur un paiement parent devenu Completed.</summary>
    void ApplyWithholdingIfDue(Tenant tenant, Payment payment);
}

public sealed class TeacherLicenseActivationService(
    IApplicationDbContext db,
    ITeacherSchoolAdminService teacherSchools,
    IPlatformPromoService promoCodes) : ITeacherLicenseActivationService
{
    public async Task<PlatformLicensePaymentStatusDto> ActivateSessionAsync(
        Guid tenantId,
        string actorUserId,
        ActivateTeacherSessionRequest request,
        bool asPlatformAdmin,
        CancellationToken ct = default)
    {
        EnsureCanActivate(tenantId, actorUserId, asPlatformAdmin);

        var settlement = (request.Settlement ?? "").Trim().ToLowerInvariant();
        if (settlement is LicenseFeeWithholding.SettlementPay)
            throw new InvalidOperationException(
                "Le paiement par carte est effectué par l'enseignant. Utilisez un code promo ou la retenue à la source.");

        if (settlement is LicenseFeeWithholding.SettlementPromo)
            return await promoCodes.RedeemForTenantAsync(
                tenantId,
                request.PromoCode ?? "",
                actorUserId,
                skipRenewalWindow: true,
                createIfMissing: asPlatformAdmin,
                autoRenewAtSource: request.AutoRenewAtSource,
                ct);

        if (settlement is LicenseFeeWithholding.SettlementWithhold)
            return await ActivateWithWithholdingAsync(tenantId, actorUserId, request.AutoRenewAtSource, ct);

        throw new InvalidOperationException(
            "Indiquez un code promo ou choisissez la retenue à la source (équivalent 10 $ USD).");
    }

    public async Task ApplySettlementOnApprovalAsync(
        Guid tenantId,
        string actorUserId,
        string? settlement,
        string? promoCode,
        bool asPlatformAdmin,
        bool autoRenewAtSource = false,
        CancellationToken ct = default)
    {
        var kind = (settlement ?? LicenseFeeWithholding.SettlementPay).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(kind) || kind == LicenseFeeWithholding.SettlementPay)
            return;

        await ActivateSessionAsync(
            tenantId,
            actorUserId,
            new ActivateTeacherSessionRequest(kind, promoCode, autoRenewAtSource),
            asPlatformAdmin,
            ct);
    }

    public void ClearWithholding(Tenant tenant)
    {
        if (tenant.LicenseFeeWithholdingRemainingUsd <= 0
            && tenant.LicenseSettlementKind != LicenseFeeWithholding.SettlementWithhold)
            return;
        tenant.LicenseFeeWithholdingRemainingUsd = 0;
        tenant.UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyWithholdingIfDue(Tenant tenant, Payment payment) =>
        LicenseFeeWithholding.TakeFromTutorShare(tenant, payment);

    private async Task<PlatformLicensePaymentStatusDto> ActivateWithWithholdingAsync(
        Guid tenantId,
        string actorUserId,
        bool autoRenewAtSource,
        CancellationToken ct)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Profil enseignant introuvable.");

        var now = DateTime.UtcNow;
        LicenseFeeWithholding.GrantLicenseYears(tenant, 1, now);
        tenant.LicenseFeeWithholdingRemainingUsd = decimal.Round(
            tenant.LicenseFeeWithholdingRemainingUsd + LicenseFeeWithholding.AnnualFeeUsd,
            2,
            MidpointRounding.AwayFromZero);
        tenant.LicenseSettlementKind = LicenseFeeWithholding.SettlementWithhold;
        tenant.LicenseAutoRenewAtSource = true;

        var payment = new PlatformLicensePayment
        {
            TenantId = tenant.Id,
            Amount = LicenseFeeWithholding.AnnualFeeUsd,
            Currency = "USD",
            Status = PaymentStatus.Completed,
            GatewayPaymentCode = $"WITHHOLD:{actorUserId}",
            PeriodStart = now,
            PeriodEnd = tenant.LicenseExpiresAt,
            CompletedAt = now
        };
        db.Add(payment);
        await db.SaveChangesAsync(ct);

        return new PlatformLicensePaymentStatusDto(
            payment.Id,
            payment.GatewayPaymentCode ?? "WITHHOLD",
            "Completed",
            payment.Status.ToString(),
            payment.CompletedAt,
            tenant.LicenseExpiresAt,
            tenant.HasValidLicense(),
            tenant.HasPaidLicense(),
            tenant.RequiresOnboarding());
    }

    private void EnsureCanActivate(Guid tenantId, string actorUserId, bool asPlatformAdmin)
    {
        if (asPlatformAdmin)
            return;
        teacherSchools.EnsureExpertCanManageTeacher(tenantId, actorUserId);
    }
}
