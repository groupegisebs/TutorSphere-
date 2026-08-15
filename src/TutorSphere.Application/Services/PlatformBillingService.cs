using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.PlatformBilling;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IPlatformBillingService
{
    Task<PlatformLicenseStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<PlatformLicenseCheckoutResponse> CreateCheckoutAsync(
        string ownerUserId,
        CreatePlatformLicenseCheckoutRequest request,
        CancellationToken ct = default);
    Task<PlatformLicensePaymentStatusDto> ConfirmAsync(
        string ownerUserId,
        Guid? paymentId = null,
        CancellationToken ct = default);
    Task ExpireOverdueLicensesAsync(CancellationToken ct = default);

    /// <summary>Rappel e-mail ~1 mois avant l'échéance de la licence.</summary>
    Task SendUpcomingRenewalRemindersAsync(CancellationToken ct = default);
}

public class PlatformBillingService(
    IApplicationDbContext db,
    IPaymentGatewayService paymentGateway,
    PlatformBillingOptions options,
    IEmailService email,
    IUserContactLookup contacts) : IPlatformBillingService
{
    public async Task<PlatformLicenseStatusDto> GetStatusForOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var tenant = RequireOwnerTenant(ownerUserId);
        await EnsureLicenseStateAsync(tenant, ct);
        return ToStatus(tenant);
    }

    public async Task<PlatformLicenseCheckoutResponse> CreateCheckoutAsync(
        string ownerUserId,
        CreatePlatformLicenseCheckoutRequest request,
        CancellationToken ct = default)
    {
        var tenant = RequireOwnerTenant(ownerUserId);
        await EnsureLicenseStateAsync(tenant, ct);

        // Renouvellement autorisé dès la fenêtre « 1 mois avant » (ou si déjà en AwaitingRenewal).
        if (tenant.HasPaidLicense()
            && tenant.Status != TenantStatus.AwaitingRenewal
            && tenant.LicenseExpiresAt is { } expires
            && expires > DateTime.UtcNow.AddDays(options.RenewalReminderDays)
            && tenant.OnboardingCompletedAt is not null)
        {
            throw new InvalidOperationException(
                "Votre session enseignant est déjà active. Le renouvellement sera disponible 1 mois avant l'échéance.");
        }

        if (tenant.HasPaidLicense() && tenant.RequiresOnboarding())
        {
            throw new InvalidOperationException(
                "Votre licence est déjà payée. Complétez l'auto-formation pour activer votre session enseignant.");
        }

        return await paymentGateway.CreatePlatformLicenseCheckoutAsync(tenant.Id, request, ct);
    }

    public async Task<PlatformLicensePaymentStatusDto> ConfirmAsync(
        string ownerUserId,
        Guid? paymentId = null,
        CancellationToken ct = default)
    {
        var tenant = RequireOwnerTenant(ownerUserId);
        var result = await paymentGateway.ConfirmPlatformLicensePaymentAsync(tenant.Id, paymentId, ct: ct);

        tenant = db.Tenants.First(t => t.Id == tenant.Id);
        return new PlatformLicensePaymentStatusDto(
            result.PaymentId,
            result.PaymentCode,
            result.GatewayStatus,
            result.LocalStatus,
            result.PaidAt,
            tenant.LicenseExpiresAt,
            tenant.HasValidLicense(),
            tenant.HasPaidLicense(),
            tenant.RequiresOnboarding());
    }

    public async Task ExpireOverdueLicensesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = db.Tenants
            .Where(t => (t.Status == TenantStatus.Active || t.Status == TenantStatus.AwaitingOnboarding)
                        && t.LicenseExpiresAt != null
                        && t.LicenseExpiresAt <= now)
            .ToList();

        foreach (var tenant in expired)
        {
            tenant.Status = TenantStatus.AwaitingRenewal;
            tenant.IsPublicProfile = false;
            tenant.UpdatedAt = now;

            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is { } c && !string.IsNullOrWhiteSpace(c.Email))
            {
                var firstName = c.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                                ?? c.DisplayName;
                await email.SendTutorRenewalReminderAsync(c.Email, firstName, tenant.LicenseExpiresAt ?? now, ct);
            }
        }

        if (expired.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public async Task SendUpcomingRenewalRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(options.RenewalReminderDays);

        var due = db.Tenants
            .Where(t => (t.Status == TenantStatus.Active || t.Status == TenantStatus.AwaitingOnboarding)
                        && t.LicenseExpiresAt != null
                        && t.LicenseExpiresAt > now
                        && t.LicenseExpiresAt <= windowEnd
                        && t.LicenseRenewalReminderSentAt == null)
            .ToList();

        foreach (var tenant in due)
        {
            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is { } c && !string.IsNullOrWhiteSpace(c.Email))
            {
                var firstName = c.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                                ?? c.DisplayName;
                await email.SendTutorRenewalReminderAsync(c.Email, firstName, tenant.LicenseExpiresAt!.Value, ct);
            }

            tenant.LicenseRenewalReminderSentAt = now;
            tenant.UpdatedAt = now;
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task EnsureLicenseStateAsync(Domain.Entities.Tenant tenant, CancellationToken ct)
    {
        if ((tenant.Status == TenantStatus.Active || tenant.Status == TenantStatus.AwaitingOnboarding)
            && tenant.LicenseExpiresAt is { } expires
            && expires <= DateTime.UtcNow)
        {
            tenant.Status = TenantStatus.AwaitingRenewal;
            tenant.IsPublicProfile = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private Domain.Entities.Tenant RequireOwnerTenant(string ownerUserId) =>
        db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
        ?? throw new InvalidOperationException("Aucun profil enseignant associé à ce compte.");

    private PlatformLicenseStatusDto ToStatus(Domain.Entities.Tenant tenant)
    {
        var now = DateTime.UtcNow;
        var paid = tenant.HasPaidLicense(now);
        var valid = tenant.HasValidLicense(now);
        int? days = tenant.LicenseExpiresAt is { } exp
            ? (int)Math.Ceiling((exp - now).TotalDays)
            : null;
        var renewalSoon = paid
                          && tenant.OnboardingCompletedAt is not null
                          && days is int d
                          && d <= options.RenewalReminderDays;

        return new PlatformLicenseStatusDto(
            tenant.Id,
            tenant.Name,
            tenant.Status.ToString(),
            valid,
            paid,
            !paid,
            tenant.RequiresOnboarding(now),
            tenant.LicenseExpiresAt,
            tenant.OnboardingCompletedAt,
            days,
            options.AnnualFeeCad,
            options.Currency,
            renewalSoon,
            tenant.LicenseFeeWithholdingRemainingUsd);
    }
}
