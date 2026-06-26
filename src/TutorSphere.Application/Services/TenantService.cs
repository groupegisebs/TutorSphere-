using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Tenants;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace TutorSphere.Application.Services;

public interface ITenantService
{
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken ct = default);
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

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Subdomain = slug,
            City = request.City,
            Country = request.Country ?? "CA",
            Status = TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            Branding = new TenantBranding()
        };

        _db.Add(tenant);
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

    private static TenantDto MapToDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        tenant.Subdomain,
        tenant.Status.ToString(),
        tenant.Plan.ToString(),
        tenant.Currency,
        tenant.Language);
}
