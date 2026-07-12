using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IBrandingService
{
    Task<TenantBrandingDto?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantBrandingDto> UpdateBrandingAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken ct = default);
    Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, CancellationToken ct = default);
    Task<PublicTutorDetailDto?> GetPublicTutorDetailAsync(string slug, CancellationToken ct = default);
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
        var tenantExists = _db.Tenants.Any(t => t.Id == tenantId);
        if (!tenantExists)
            throw new InvalidOperationException("École introuvable.");

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenantId);
        if (branding is null)
        {
            branding = new TenantBranding { TenantId = tenantId };
            _db.Add(branding);
        }

        if (request.LogoUrl is not null)
            branding.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        if (request.BannerUrl is not null)
            branding.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.PrimaryColor))
            branding.PrimaryColor = NormalizeColor(request.PrimaryColor, branding.PrimaryColor);
        if (!string.IsNullOrWhiteSpace(request.SecondaryColor))
            branding.SecondaryColor = NormalizeColor(request.SecondaryColor, branding.SecondaryColor);
        if (request.Presentation is not null)
            branding.Presentation = string.IsNullOrWhiteSpace(request.Presentation) ? null : request.Presentation.Trim();
        if (request.Portfolio is not null)
            branding.Portfolio = string.IsNullOrWhiteSpace(request.Portfolio) ? null : request.Portfolio.Trim();
        branding.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(branding);
    }

    public Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();
        var tenant = _db.Tenants
            .Where(t => t.Slug == normalizedSlug || t.Subdomain == normalizedSlug)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.City,
                t.Country
            })
            .FirstOrDefault();

        if (tenant is null)
            return Task.FromResult<PublicTenantSiteDto?>(null);

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
        var offerings = _db.SubscriptionOfferings
            .Where(o => o.TenantId == tenant.Id && o.IsActive)
            .OrderBy(o => o.Title)
            .ToList();

        var site = new PublicTenantSiteDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Description,
            tenant.City,
            tenant.Country,
            branding is null
                ? new TenantBrandingDto(Guid.Empty, tenant.Id, null, null, "#2563eb", "#1e40af", null, null)
                : MapToDto(branding),
            offerings.Select(o => new PublicOfferingDto(
                o.Id,
                o.Title,
                o.Description,
                o.Subject,
                o.Price,
                o.Currency,
                o.DurationDays,
                o.SessionCount,
                o.Frequency,
                o.Mode.ToString())).ToList());

        return Task.FromResult<PublicTenantSiteDto?>(site);
    }

    public Task<PublicTutorDetailDto?> GetPublicTutorDetailAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();
        var tenant = _db.Tenants
            .Where(t => t.Slug == normalizedSlug || t.Subdomain == normalizedSlug)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.City,
                t.Country,
                t.Language,
                t.Currency,
                t.IsPublicProfile,
                t.OwnerUserId
            })
            .FirstOrDefault();

        if (tenant is null || !tenant.IsPublicProfile)
            return Task.FromResult<PublicTutorDetailDto?>(null);

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
        var offerings = _db.SubscriptionOfferings
            .Where(o => o.TenantId == tenant.Id && o.IsActive)
            .OrderBy(o => o.Title)
            .ToList();

        var portfolio = ParsePortfolio(branding?.Portfolio);
        var subjects = portfolio.Subjects.Count > 0
            ? portfolio.Subjects
            : offerings
                .Where(o => !string.IsNullOrWhiteSpace(o.Subject))
                .Select(o => o.Subject!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var detail = new PublicTutorDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Description,
            tenant.City,
            tenant.Country,
            tenant.Language,
            tenant.Currency,
            tenant.IsPublicProfile,
            string.IsNullOrWhiteSpace(tenant.OwnerUserId) ? null : tenant.OwnerUserId,
            null,
            null,
            null,
            branding?.LogoUrl,
            branding?.BannerUrl,
            branding?.PrimaryColor ?? "#2563eb",
            branding?.SecondaryColor ?? "#1e40af",
            branding?.Presentation,
            portfolio.YearsExperience,
            portfolio.HourlyRate,
            portfolio.Status,
            portfolio.Diplomas,
            portfolio.Certifications,
            subjects,
            portfolio.Availability,
            offerings.Select(o => new PublicOfferingDto(
                o.Id,
                o.Title,
                o.Description,
                o.Subject,
                o.Price,
                o.Currency,
                o.DurationDays,
                o.SessionCount,
                o.Frequency,
                FormatMode(o.Mode))).ToList());

        return Task.FromResult<PublicTutorDetailDto?>(detail);
    }
    private static PortfolioParsed ParsePortfolio(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PortfolioParsed.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new PortfolioParsed(
                ReadInt(root, "yearsExperience"),
                ReadDecimal(root, "hourlyRate"),
                ReadString(root, "status"),
                ReadCredentials(root, "diplomas"),
                ReadCredentials(root, "certifications"),
                ReadStringList(root, "subjects"),
                ReadStringList(root, "availability"));
        }
        catch (JsonException)
        {
            return PortfolioParsed.Empty;
        }
    }

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : 0;

    private static decimal ReadDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : 0m;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static List<string> ReadStringList(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
            return [];

        return p.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();
    }

    private static List<PublicCredentialDto> ReadCredentials(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<PublicCredentialDto>();
        foreach (var item in p.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var institution = item.TryGetProperty("institution", out var i) ? i.GetString() : null;
            var year = item.TryGetProperty("year", out var y) ? y.GetString() : null;
            list.Add(new PublicCredentialDto(title.Trim(), institution, year));
        }

        return list;
    }

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "En personne",
        LessonMode.Online => "En ligne",
        LessonMode.Hybrid => "Hybride",
        _ => mode.ToString()
    };

    private sealed record PortfolioParsed(
        int YearsExperience,
        decimal HourlyRate,
        string? Status,
        List<PublicCredentialDto> Diplomas,
        List<PublicCredentialDto> Certifications,
        List<string> Subjects,
        List<string> Availability)
    {
        public static PortfolioParsed Empty { get; } = new(0, 0, null, [], [], [], []);
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
