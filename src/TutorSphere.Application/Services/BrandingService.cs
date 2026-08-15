using System.Text.Json;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IBrandingService
{
    Task<TenantBrandingDto?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantBrandingDto> UpdateBrandingAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken ct = default);
    Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, string? viewerCountry = null, CancellationToken ct = default);
    Task<TeacherPublicProfileDto?> GetPublicTutorDetailAsync(string slug, string? viewerCountry = null, CancellationToken ct = default);
}

public class BrandingService : IBrandingService
{
    private readonly IApplicationDbContext _db;
    private readonly ITeacherPublicIdentityLookup _identities;

    public BrandingService(IApplicationDbContext db, ITeacherPublicIdentityLookup identities)
    {
        _db = db;
        _identities = identities;
    }

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
            throw new InvalidOperationException("Profil introuvable.");

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
            branding.Presentation = string.IsNullOrWhiteSpace(request.Presentation)
                ? null
                : TeacherContactPrivacy.RedactFromPublicText(request.Presentation.Trim());
        if (request.Portfolio is not null)
            branding.Portfolio = string.IsNullOrWhiteSpace(request.Portfolio) ? null : request.Portfolio.Trim();
        branding.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(branding);
    }

    public Task<PublicTenantSiteDto?> GetPublicSiteBySlugAsync(string slug, string? viewerCountry = null, CancellationToken ct = default)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();
        var now = DateTime.UtcNow;
        var tenant = _db.Tenants
            .Where(t => (t.Slug == normalizedSlug || t.Subdomain == normalizedSlug)
                        && t.IsPublicProfile
                        && t.Status == TenantStatus.Active
                        && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.OnboardingCompletedAt != null
                        && t.LicenseExpiresAt != null
                        && t.LicenseExpiresAt > now)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.City,
                t.Country,
                t.VisibleCountryCodes
            })
            .FirstOrDefault();

        if (tenant is null)
            return Task.FromResult<PublicTenantSiteDto?>(null);

        if (!ProfileVisibility.IsVisibleTo(tenant.VisibleCountryCodes, tenant.Country, viewerCountry))
            return Task.FromResult<PublicTenantSiteDto?>(null);

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.TenantId == tenant.Id && o.IsActive)
            .OrderBy(o => o.Title)
            .ToList();

        var brandingDto = branding is null
            ? new TenantBrandingDto(Guid.Empty, tenant.Id, null, null, "#2563eb", "#1e40af", null, null)
            : MapToPublicBranding(branding);

        var site = new PublicTenantSiteDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            TeacherContactPrivacy.RedactFromPublicText(tenant.Description),
            tenant.City,
            tenant.Country,
            brandingDto,
            offerings.Select(o => new PublicOfferingDto(
                o.Id,
                o.Title,
                TeacherContactPrivacy.RedactFromPublicText(o.Description),
                o.Subject,
                o.Price,
                o.Currency,
                o.DurationDays,
                o.SessionCount,
                o.Frequency,
                o.Mode.ToString())).ToList());

        return Task.FromResult<PublicTenantSiteDto?>(site);
    }

    public async Task<TeacherPublicProfileDto?> GetPublicTutorDetailAsync(string slug, string? viewerCountry = null, CancellationToken ct = default)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();
        var now = DateTime.UtcNow;
        var tenant = _db.Tenants
            .Where(t => (t.Slug == normalizedSlug || t.Subdomain == normalizedSlug)
                        && t.IsPublicProfile
                        && t.Status == TenantStatus.Active
                        && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.OnboardingCompletedAt != null
                        && t.LicenseExpiresAt != null
                        && t.LicenseExpiresAt > now)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                t.Description,
                t.City,
                t.Country,
                t.VisibleCountryCodes,
                t.Language,
                t.Currency,
                t.OwnerUserId,
                t.ApprovedByExpertGroupId
            })
            .FirstOrDefault();

        if (tenant is null)
            return null;

        if (!ProfileVisibility.IsVisibleTo(tenant.VisibleCountryCodes, tenant.Country, viewerCountry))
            return null;

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
        ExpertGroup? approvedGroup = null;
        if (tenant.ApprovedByExpertGroupId is Guid gid)
            approvedGroup = _db.ExpertGroups.FirstOrDefault(g => g.Id == gid);

        var assignedDisciplineIds = _db.TeacherDisciplineAssignments
            .Where(a => a.TenantId == tenant.Id)
            .Select(a => a.DisciplineId)
            .ToList();
        var publicDisciplines = new List<PublicDisciplineDto>();
        if (assignedDisciplineIds.Count > 0)
        {
            var disciplineRows = _db.Disciplines
                .Where(d => assignedDisciplineIds.Contains(d.Id) && d.IsActive)
                .OrderBy(d => d.Cycle).ThenBy(d => d.Name)
                .ToList();
            var disciplineIds = disciplineRows.Select(d => d.Id).ToList();
            var serviceRows = disciplineIds.Count == 0
                ? []
                : _db.DisciplineServiceItems
                    .Where(s => disciplineIds.Contains(s.DisciplineId))
                    .OrderBy(s => s.SortOrder)
                    .ToList();
            publicDisciplines = disciplineRows.Select(d => new PublicDisciplineDto(
                d.Id,
                d.Name,
                CycleLabel(d.Cycle),
                d.WorkMethod,
                serviceRows.Where(s => s.DisciplineId == d.Id)
                    .Select(s => new PublicDisciplineServiceDto(
                        s.Title,
                        TeacherContactPrivacy.RedactFromPublicText(s.Description)))
                    .ToList())).ToList();
        }
        var offerings = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.TenantId == tenant.Id && o.IsActive)
            .OrderBy(o => o.Title)
            .ToList();

        var portfolio = ParsePortfolio(TeacherContactPrivacy.StripPortfolioPii(branding?.Portfolio));

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

        var fullBio = TeacherContactPrivacy.RedactFromPublicText(
            FirstNonEmpty(branding?.Presentation, tenant.Description));
        var shortBio = TeacherContactPrivacy.RedactFromPublicText(
            FirstNonEmpty(tenant.Description, branding?.Presentation));

        var publicOfferings = offerings.Select(o =>
        {
            var slots = ExtractAvailabilityFromConditions(o.Conditions);
            return new PublicOfferingDto(
                o.Id,
                o.Title,
                TeacherContactPrivacy.RedactFromPublicText(o.Description),
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

        TeacherPublicNameParts? names = null;
        if (!string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            var map = await _identities.GetByUserIdsAsync([tenant.OwnerUserId], ct);
            map.TryGetValue(tenant.OwnerUserId, out names);
        }

        var given = TeacherPublicName.FirstToken(names?.FirstName);
        if (given.Length == 0)
            given = TeacherPublicName.FirstToken(tenant.Name);
        var display = TeacherPublicName.Format(names?.FirstName, names?.LastName, tenant.Name);
        var initials = TeacherPublicName.Initials(names?.FirstName, names?.LastName, tenant.Name);
        var photo = TeacherPublicPhotoResolver.Resolve(branding?.LogoUrl, approvedGroup?.LogoUrl, initials);
        var languages = new List<string>();
        if (!string.IsNullOrWhiteSpace(tenant.Language))
            languages.Add(tenant.Language.Trim());
        foreach (var lang in portfolio.Languages)
        {
            if (languages.All(x => !string.Equals(x, lang, StringComparison.OrdinalIgnoreCase)))
                languages.Add(lang);
        }

        var primary = ColorHex.Normalize(approvedGroup?.PrimaryColor, ColorHex.TutorSpherePrimary);
        var secondary = ColorHex.Normalize(approvedGroup?.SecondaryColor, ColorHex.TutorSphereSecondary);

        return new TeacherPublicProfileDto
        {
            Slug = tenant.Slug,
            DisplayName = string.IsNullOrWhiteSpace(display) ? given : display,
            GivenName = given,
            PublicInitials = initials,
            Location = TeacherPublicName.GeneralLocation(tenant.City, tenant.Country),
            City = TeacherPublicName.GeneralLocation(tenant.City, null),
            Country = TeacherPublicName.GeneralLocation(null, tenant.Country),
            Language = tenant.Language,
            Currency = tenant.Currency,
            PhotoUrl = photo.Url,
            PhotoKind = TeacherPublicPhotoResolver.ToApi(photo.Kind),
            PhotoIsGroupLogoFallback = photo.IsGroupLogoFallback,
            BannerUrl = string.IsNullOrWhiteSpace(approvedGroup?.BannerUrl) ? null : approvedGroup.BannerUrl,
            PrimaryColor = primary,
            SecondaryColor = secondary,
            ShortBio = shortBio,
            FullBio = fullBio,
            YearsExperience = portfolio.YearsExperience,
            HourlyRate = portfolio.HourlyRate,
            Status = TeacherContactPrivacy.RedactFromPublicText(portfolio.Status),
            Diplomas = portfolio.Diplomas,
            Certifications = portfolio.Certifications,
            Subjects = subjects,
            Levels = levels,
            Languages = languages,
            Availability = availability,
            Offerings = publicOfferings,
            ExpertGroupName = approvedGroup?.Name,
            ExpertGroupLogoUrl = approvedGroup?.LogoUrl,
            ExpertGroupCountryCode = approvedGroup?.CountryCode,
            IsVerified = tenant.ApprovedByExpertGroupId is not null,
            Rating = null,
            ReviewCount = 0,
            Disciplines = publicDisciplines
        };
    }

    private static string CycleLabel(SchoolCycle cycle) => cycle switch
    {
        SchoolCycle.Primary => "Primaire",
        SchoolCycle.Secondary => "Secondaire",
        SchoolCycle.University => "Universitaire",
        SchoolCycle.AdultEducation => "Formation pour adultes",
        _ => cycle.ToString()
    };

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
                ReadStringList(root, "availability", "Availability"),
                ReadStringList(root, "languages", "Languages"));
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
        List<string> Availability,
        List<string> Languages)
    {
        public static PortfolioParsed Empty { get; } = new(0, 0, null, [], [], [], [], [], []);
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

    private static TenantBrandingDto MapToPublicBranding(TenantBranding branding)
    {
        var dto = MapToDto(branding);
        return dto with
        {
            Presentation = TeacherContactPrivacy.RedactFromPublicText(dto.Presentation),
            Portfolio = null
        };
    }

    private static string NormalizeColor(string? color, string fallback) =>
        ColorHex.Normalize(color, fallback);
}
