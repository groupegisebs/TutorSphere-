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

        // Renouvellement seulement si licence payée encore longue (même si formation en cours).
        if (tenant.HasPaidLicense()
            && tenant.LicenseExpiresAt is { } expires
            && expires > DateTime.UtcNow.AddDays(options.RenewalReminderDays)
            && tenant.OnboardingCompletedAt is not null)
        {
            throw new InvalidOperationException(
                "Votre établissement est déjà actif. Le renouvellement sera disponible près de l'échéance.");
        }

        if (tenant.HasPaidLicense() && tenant.RequiresOnboarding())
        {
            throw new InvalidOperationException(
                "Votre licence est déjà payée. Complétez l'auto-formation pour activer votre établissement.");
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
        ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");

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
            renewalSoon);
    }
}
