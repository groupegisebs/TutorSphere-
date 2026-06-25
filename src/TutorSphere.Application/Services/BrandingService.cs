using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IBrandingService
{
    Task<TenantBrandingDto?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantBrandingDto> UpdateBrandingAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken ct = default);
    Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, CancellationToken ct = default);
}

public class BrandingService : IBrandingService
{
    private readonly IApplicationDbContext _db;

    public BrandingService(IApplicationDbContext db) => _db = db;

    public Task<TenantBrandingDto?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default)
    {
        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenantId);
        return Task.FromResult(branding is null ? null : MapToDto(branding));
    }

    public async Task<TenantBrandingDto> UpdateBrandingAsync(
        Guid tenantId,
        UpdateTenantBrandingRequest request,
        CancellationToken ct = default)
    {
        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenantId)
            ?? throw new InvalidOperationException("Personnalisation introuvable pour cette école.");

        branding.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        branding.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        branding.PrimaryColor = NormalizeColor(request.PrimaryColor, "#2563eb");
        branding.SecondaryColor = NormalizeColor(request.SecondaryColor, "#1e40af");
        branding.Presentation = string.IsNullOrWhiteSpace(request.Presentation) ? null : request.Presentation.Trim();
        branding.Portfolio = string.IsNullOrWhiteSpace(request.Portfolio) ? null : request.Portfolio.Trim();
        branding.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(branding);
    }

    public Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();
        var site = _db.Tenants
            .Where(t => t.Slug == normalizedSlug || t.Subdomain == normalizedSlug)
            .Select(t => new PublicTenantSiteDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.City,
                t.Country,
                t.Branding == null
                    ? new TenantBrandingDto(Guid.Empty, t.Id, null, null, "#2563eb", "#1e40af", null, null)
                    : new TenantBrandingDto(
                        t.Branding.Id,
                        t.Branding.TenantId,
                        t.Branding.LogoUrl,
                        t.Branding.BannerUrl,
                        t.Branding.PrimaryColor,
                        t.Branding.SecondaryColor,
                        t.Branding.Presentation,
                        t.Branding.Portfolio),
                t.Offerings
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Title)
                    .Select(o => new PublicOfferingDto(
                        o.Id,
                        o.Title,
                        o.Description,
                        o.Subject,
                        o.Price,
                        o.Currency,
                        o.DurationDays,
                        o.SessionCount,
                        o.Frequency,
                        o.Mode.ToString()))
                    .ToList()))
            .FirstOrDefault();

        return Task.FromResult(site);
    }

    private static TenantBrandingDto MapToDto(TenantBranding branding) => new(
        branding.Id,
        branding.TenantId,
        branding.LogoUrl,
        branding.BannerUrl,
        branding.PrimaryColor,
        branding.SecondaryColor,
        branding.Presentation,
        branding.Portfolio);

    private static string NormalizeColor(string? color, string fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
            return fallback;

        var trimmed = color.Trim();
        return trimmed.StartsWith('#') && (trimmed.Length == 7 || trimmed.Length == 4)
            ? trimmed
            : fallback;
    }
}
