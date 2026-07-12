using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Tenants;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Payouts;
using Microsoft.Extensions.Logging;

namespace TutorSphere.Application.Services;

public interface ITenantService
{
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken ct = default);
    Task<TutorProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
    Task<TutorProfileDto> UpdateProfileAsync(Guid tenantId, UpdateTutorProfileRequest request, CancellationToken ct = default);
}

public class TenantService : ITenantService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        IApplicationDbContext db,
        IEmailService email,
        ILogger<TenantService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.ToLowerInvariant().Trim();
        if (_db.Tenants.Any(t => t.Slug == slug))
            throw new InvalidOperationException("Ce slug est déjà utilisé.");

        var country = TutorPayoutPolicy.NormalizeCountry(request.Country ?? "CA");
        var region = TutorPayoutPolicy.ResolveRegion(country);

        if (TutorPayoutPolicy.RequiresPayPalAtSignup(country))
        {
            if (string.IsNullOrWhiteSpace(request.PayPalEmail) || !request.PayPalEmail.Contains('@'))
                throw new InvalidOperationException(
                    "Un compte PayPal (adresse e-mail) est obligatoire à la création du compte enseignant.");
        }

        if (TutorPayoutPolicy.RequiresStripeAtSignup(country)
            && string.IsNullOrWhiteSpace(request.StripeAccountId))
        {
            // Stripe Connect peut être complété juste après via l'interface d'onboarding,
            // mais on enregistre le champ s'il est fourni.
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Subdomain = slug,
            City = request.City,
            Country = country,
            Status = TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            PayPalEmail = string.IsNullOrWhiteSpace(request.PayPalEmail)
                ? null
                : request.PayPalEmail.Trim().ToLowerInvariant(),
            StripeAccountId = string.IsNullOrWhiteSpace(request.StripeAccountId)
                ? null
                : request.StripeAccountId.Trim(),
            Branding = new TenantBranding()
        };

        _db.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // Comptes de versement initiaux
        if (!string.IsNullOrWhiteSpace(tenant.PayPalEmail))
        {
            _db.Add(new TutorPayoutAccount
            {
                TenantId = tenant.Id,
                Label = "PayPal",
                ProviderKind = PayoutProviderKind.PayPal,
                CountryCode = country,
                Currency = TutorPayoutPolicy.PolicyCurrency,
                AccountHolderName = $"{request.OwnerFirstName} {request.OwnerLastName}".Trim(),
                EmailOrAccountId = tenant.PayPalEmail,
                IsPrimary = region != PayoutRegionKind.StripeConnectZone || string.IsNullOrWhiteSpace(tenant.StripeAccountId),
                IsActive = true
            });
        }

        if (!string.IsNullOrWhiteSpace(tenant.StripeAccountId))
        {
            _db.Add(new TutorPayoutAccount
            {
                TenantId = tenant.Id,
                Label = "Stripe Connect",
                ProviderKind = PayoutProviderKind.StripeConnect,
                CountryCode = country,
                Currency = TutorPayoutPolicy.PolicyCurrency,
                AccountHolderName = $"{request.OwnerFirstName} {request.OwnerLastName}".Trim(),
                EmailOrAccountId = tenant.StripeAccountId,
                IsPrimary = true,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            await _email.SendSchoolCreatedAsync(
                ownerEmail: request.OwnerEmail,
                ownerFirstName: request.OwnerFirstName,
                schoolName: tenant.Name,
                ct: ct);
        }

        return MapToDto(tenant);
    }

    public Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Slug == slug.ToLowerInvariant());
        return Task.FromResult(tenant is null ? null : MapToDto(tenant));
    }

    public Task<TenantDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var revenue = _db.Payments
            .Where(p => p.TenantId == tenantId && p.Status == PaymentStatus.Completed && p.CompletedAt >= monthStart)
            .Sum(p => (decimal?)p.TutorAmount) ?? 0m;

        var activeStudents = _db.Students.Count(s => s.TenantId == tenantId && s.IsActive);
        var activeSubscriptions = _db.StudentSubscriptions.Count(s =>
            s.TenantId == tenantId && s.Status == SubscriptionStatus.Active);
        var upcomingLessons = _db.Lessons.Count(l => l.TenantId == tenantId && l.StartTime > now);
        var pendingPayments = _db.Payments.Count(p =>
            p.TenantId == tenantId && p.Status == PaymentStatus.Pending);

        return Task.FromResult(new TenantDashboardDto(
            revenue, activeStudents, activeSubscriptions, upcomingLessons, pendingPayments));
    }

    public Task<TutorProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        return Task.FromResult(tenant is null ? null : MapToProfileDto(tenant));
    }

    public async Task<TutorProfileDto> UpdateProfileAsync(Guid tenantId, UpdateTutorProfileRequest request, CancellationToken ct = default)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("Locataire introuvable.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name.Trim();
        if (request.Description is not null)
            tenant.Description = request.Description.Trim();
        if (request.City is not null)
            tenant.City = request.City.Trim();
        if (request.Country is not null)
            tenant.Country = request.Country.Trim();
        if (!string.IsNullOrWhiteSpace(request.Language))
            tenant.Language = request.Language.Trim();
        if (!string.IsNullOrWhiteSpace(request.Currency))
            tenant.Currency = request.Currency.Trim();

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToProfileDto(tenant);
    }

    private static TenantDto MapToDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        tenant.Subdomain,
        tenant.Status.ToString(),
        tenant.Plan.ToString(),
        tenant.Currency,
        tenant.Language);

    private static TutorProfileDto MapToProfileDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Description,
        tenant.City,
        tenant.Country,
        tenant.Language,
        tenant.Currency,
        tenant.Slug);
}
