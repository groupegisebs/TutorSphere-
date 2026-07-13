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
        var offerings = _db.SubscriptionOfferingsForAnyTenant
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
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.TenantId == tenant.Id && o.IsActive)
            .OrderBy(o => o.Title)
            .ToList();

        var portfolio = ParsePortfolio(branding?.Portfolio);

        var offeringSubjects = offerings
            .SelectMany(o => ExtractSubjects(o.Subject, o.Title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subjects = portfolio.Subjects
            .Concat(offeringSubjects)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var offeringLevels = offerings
            .Select(o => ExtractLevelFromConditions(o.Conditions))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !IsAllLevels(l))
            .Cast<string>()
            .ToList();

        var levels = portfolio.Levels
            .Concat(offeringLevels)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var offeringAvailability = offerings
            .SelectMany(o => ExtractAvailabilityFromConditions(o.Conditions))
            .ToList();

        var availability = portfolio.Availability
            .Concat(offeringAvailability)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Presentation = approche / CV narratif; Description école = résumé court.
        var fullBio = FirstNonEmpty(branding?.Presentation, tenant.Description);
        var shortBio = FirstNonEmpty(tenant.Description, branding?.Presentation);

        var publicOfferings = offerings.Select(o =>
        {
            var slots = ExtractAvailabilityFromConditions(o.Conditions);
            return new PublicOfferingDto(
                o.Id,
                o.Title,
                o.Description,
                string.IsNullOrWhiteSpace(o.Subject)
                    ? ExtractSubjects(o.Subject, o.Title).FirstOrDefault()
                    : o.Subject,
                o.Price,
                o.Currency,
                o.DurationDays,
                o.SessionCount,
                o.Frequency,
                FormatMode(o.Mode),
                string.IsNullOrWhiteSpace(o.Frequency) ? null : o.Frequency,
                slots);
        }).ToList();

        var detail = new PublicTutorDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            shortBio,
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
            fullBio,
            portfolio.YearsExperience,
            portfolio.HourlyRate,
            portfolio.Status,
            portfolio.Diplomas,
            portfolio.Certifications,
            subjects,
            levels,
            availability,
            publicOfferings);

        return Task.FromResult<PublicTutorDetailDto?>(detail);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static IEnumerable<string> ExtractSubjects(string? subject, string? title)
    {
        if (!string.IsNullOrWhiteSpace(subject))
            yield return subject.Trim();

        if (string.IsNullOrWhiteSpace(title))
            yield break;

        // Common pattern: "Pack Mathématiques — Collège"
        var separators = new[] { "—", "-", ":", "|" };
        foreach (var sep in separators)
        {
            var idx = title.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var left = title[..idx].Trim();
                if (left.Length is > 2 and < 40)
                    yield return left.StartsWith("Pack ", StringComparison.OrdinalIgnoreCase)
                        ? left["Pack ".Length..].Trim()
                        : left;
                yield break;
            }
        }
    }

    private static List<string> ExtractAvailabilityFromConditions(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(conditions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return [];
            if (!doc.RootElement.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<string>();
            foreach (var slot in slots.EnumerateArray())
            {
                if (slot.ValueKind != JsonValueKind.Object)
                    continue;
                var day = slot.TryGetProperty("day", out var d) ? d.GetString() : null;
                var time = slot.TryGetProperty("time", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(day) || string.IsNullOrWhiteSpace(time))
                    continue;
                list.Add($"{day.Trim()}-{time.Trim()}");
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
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
                ReadInt(root, "yearsExperience", "YearsExperience"),
                ReadDecimal(root, "hourlyRate", "HourlyRate"),
                ReadString(root, "status", "Status"),
                ReadCredentials(root, "diplomas", "Diplomas"),
                ReadCredentials(root, "certifications", "Certifications"),
                ReadStringList(root, "subjects", "Subjects"),
                ReadStringList(root, "levels", "Levels"),
                ReadStringList(root, "availability", "Availability"));
        }
        catch (JsonException)
        {
            return PortfolioParsed.Empty;
        }
    }

    private static string? ExtractLevelFromConditions(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(conditions);
            if (TryGetProperty(doc.RootElement, out var levelEl, "level", "Level")
                && levelEl.ValueKind == JsonValueKind.String)
            {
                var level = levelEl.GetString();
                return string.IsNullOrWhiteSpace(level) ? null : level.Trim();
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }

        return null;
    }

    private static bool IsAllLevels(string? level) =>
        string.Equals(level?.Trim(), "Tous niveaux", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out value))
                return true;
        }

        value = default;
        return false;
    }

    private static int ReadInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
                return v;
        }
        return 0;
    }

    private static decimal ReadDecimal(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v))
                return v;
        }
        return 0m;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }

    private static List<string> ReadStringList(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                continue;

            return p.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToList();
        }

        return [];
    }

    private static List<PublicCredentialDto> ReadCredentials(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                continue;

            var list = new List<PublicCredentialDto>();
            foreach (var item in p.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var title = ReadString(item, "title", "Title");
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var institution = ReadString(item, "institution", "Institution");
                var year = ReadString(item, "year", "Year");
                list.Add(new PublicCredentialDto(title.Trim(), institution, year));
            }

            return list;
        }

        return [];
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
        List<string> Levels,
        List<string> Availability)
    {
        public static PortfolioParsed Empty { get; } = new(0, 0, null, [], [], [], [], []);
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
