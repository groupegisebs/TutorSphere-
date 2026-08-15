using System.Text.Json;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Search;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ISearchService
{
    Task<IReadOnlyList<TutorSearchResultDto>> SearchTutorsAsync(
        TutorSearchFilters filters,
        CancellationToken ct = default);

    Task<IReadOnlyList<ExpertGroupSearchOptionDto>> ListActiveExpertGroupsAsync(
        CancellationToken ct = default);
}

public class SearchService : ISearchService
{
    private static readonly JsonSerializerOptions ScheduleJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext _db;
    private readonly ITeacherPublicIdentityLookup _identities;

    public SearchService(IApplicationDbContext db, ITeacherPublicIdentityLookup identities)
    {
        _db = db;
        _identities = identities;
    }

    public async Task<IReadOnlyList<TutorSearchResultDto>> SearchTutorsAsync(
        TutorSearchFilters filters,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Annuaire parent/élève : uniquement les enseignants avec au moins une offre active.
        var query = _db.Tenants
            .Where(t => t.Status == TenantStatus.Active
                        && t.IsPublicProfile
                        && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.OnboardingCompletedAt != null
                        && t.LicenseExpiresAt != null
                        && t.LicenseExpiresAt > now);

        if (filters.ExpertGroupId is Guid expertGroupId)
            query = query.Where(t => t.ApprovedByExpertGroupId == expertGroupId);

        if (!string.IsNullOrWhiteSpace(filters.City))
        {
            var city = filters.City.Trim();
            query = query.Where(t => t.City != null && t.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(filters.Language))
        {
            var language = filters.Language.Trim().ToLowerInvariant();
            query = query.Where(t => t.Language.ToLower() == language);
        }

        var tenants = query.ToList();

        // Pays optionnel : si fourni, restreindre à la visibilité géographique ; sinon tous.
        if (!string.IsNullOrWhiteSpace(filters.ViewerCountry)
            && ProfileVisibility.NormalizeCode(filters.ViewerCountry).Length == 2)
        {
            tenants = tenants
                .Where(t => ProfileVisibility.IsVisibleTo(t.VisibleCountryCodes, t.Country, filters.ViewerCountry))
                .ToList();
        }

        if (tenants.Count == 0)
            return [];

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var offeringsQuery = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.IsActive && tenantIds.Contains(o.TenantId));

        if (!string.IsNullOrWhiteSpace(filters.Subject))
        {
            var subject = filters.Subject.Trim();
            offeringsQuery = offeringsQuery.Where(o =>
                o.Subject != null && o.Subject.Contains(subject));
        }

        if (filters.MinPrice.HasValue)
            offeringsQuery = offeringsQuery.Where(o => o.Price >= filters.MinPrice.Value);

        if (filters.MaxPrice.HasValue)
            offeringsQuery = offeringsQuery.Where(o => o.Price <= filters.MaxPrice.Value);

        if (filters.Mode.HasValue)
            offeringsQuery = offeringsQuery.Where(o => o.Mode == filters.Mode.Value);

        var offerings = offeringsQuery.ToList();
        var offeringsByTenant = offerings
            .GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Toujours exclure les enseignants sans offre active (et ceux qui ne matchent pas les filtres d'offre).
        tenants = tenants.Where(t => offeringsByTenant.ContainsKey(t.Id)).ToList();
        if (tenants.Count == 0)
            return [];
        tenantIds = tenants.Select(t => t.Id).ToList();

        var groupIds = tenants
            .Where(t => t.ApprovedByExpertGroupId.HasValue)
            .Select(t => t.ApprovedByExpertGroupId!.Value)
            .Distinct()
            .ToList();
        var groups = _db.ExpertGroups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.LogoUrl, g.BannerUrl, g.PrimaryColor, g.SecondaryColor })
            .ToList()
            .ToDictionary(g => g.Id);

        var ownerIds = tenants
            .Select(t => t.OwnerUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct()
            .ToList();
        var ownerNames = await _identities.GetByUserIdsAsync(ownerIds, ct);

        var brandings = _db.TenantBrandings
            .Where(b => tenantIds.Contains(b.TenantId))
            .Select(b => new { b.TenantId, b.LogoUrl, b.Portfolio, b.Presentation })
            .ToList();

        var logosByTenant = brandings
            .Where(b => !string.IsNullOrWhiteSpace(b.LogoUrl))
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.First().LogoUrl!.Trim());

        var presentationByTenant = brandings
            .Where(b => !string.IsNullOrWhiteSpace(b.Presentation))
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.First().Presentation!.Trim());

        var portfolioByTenant = brandings
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => ParsePortfolioExtras(g.First().Portfolio));

        var levelFilter = filters.Level?.Trim();
        var subjectFilter = filters.Subject?.Trim();

        var results = tenants
            .Select(t =>
            {
                offeringsByTenant.TryGetValue(t.Id, out var tenantOfferings);
                tenantOfferings ??= [];
                logosByTenant.TryGetValue(t.Id, out var teacherPhotoUrl);
                portfolioByTenant.TryGetValue(t.Id, out var portfolio);
                portfolio ??= PortfolioExtras.Empty;
                presentationByTenant.TryGetValue(t.Id, out var presentation);

                var group = t.ApprovedByExpertGroupId is Guid gid && groups.TryGetValue(gid, out var g) ? g : null;
                TeacherPublicNameParts? names = null;
                if (!string.IsNullOrWhiteSpace(t.OwnerUserId))
                    ownerNames.TryGetValue(t.OwnerUserId, out names);

                var displayName = TeacherPublicName.Format(names?.FirstName, names?.LastName, t.Name);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = TeacherPublicName.FirstToken(t.Name);
                var initials = TeacherPublicName.Initials(names?.FirstName, names?.LastName, t.Name);
                var photo = TeacherPublicPhotoResolver.Resolve(teacherPhotoUrl, group?.LogoUrl, initials);

                var offeringLevels = tenantOfferings
                    .Select(o => ExtractOfferingLevel(o.Conditions))
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !IsAllLevels(l))
                    .Cast<string>();

                var levels = portfolio.Levels
                    .Concat(offeringLevels)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var subjects = tenantOfferings
                    .Select(o => o.Subject)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList();

                // Sans filtre matière : enrichir avec le portfolio ; avec filtre : déjà restreint via offres.
                if (string.IsNullOrWhiteSpace(subjectFilter))
                {
                    foreach (var s in portfolio.Subjects)
                    {
                        if (subjects.All(sub => !string.Equals(sub, s, StringComparison.OrdinalIgnoreCase)))
                            subjects.Add(s);
                    }
                }

                var specialties = portfolio.Subjects
                    .Where(s => subjects.All(sub => !string.Equals(sub, s, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var durations = tenantOfferings
                    .Select(o => ExtractSessionDuration(o.Conditions))
                    .Where(d => d is > 0)
                    .Select(d => d!.Value)
                    .ToList();

                int? sessionDuration = durations.Count > 0
                    ? (int)Math.Round(durations.Average())
                    : null;

                var modes = tenantOfferings
                    .Select(o => FormatMode(o.Mode))
                    .Distinct()
                    .ToList();

                var hasFlexible = tenantOfferings.Any(o =>
                    o.Mode is LessonMode.Online or LessonMode.Hybrid);

                var languages = new List<string>();
                if (!string.IsNullOrWhiteSpace(t.Language))
                    languages.Add(t.Language.Trim());
                foreach (var lang in portfolio.Languages)
                {
                    if (languages.All(x => !string.Equals(x, lang, StringComparison.OrdinalIgnoreCase)))
                        languages.Add(lang);
                }

                var blurb = TeacherContactPrivacy.RedactFromPublicText(
                    !string.IsNullOrWhiteSpace(t.Description)
                        ? t.Description
                        : presentation);

                decimal? minPrice = tenantOfferings.Count > 0 ? tenantOfferings.Min(o => o.Price) : null;
                decimal? maxPrice = tenantOfferings.Count > 0 ? tenantOfferings.Max(o => o.Price) : null;

                var primaryOffer = tenantOfferings.OrderBy(o => o.Price).FirstOrDefault();
                var offerTitle = TeacherContactPrivacy.RedactFromPublicText(primaryOffer?.Title);
                var offerMode = primaryOffer is null ? (modes.FirstOrDefault() ?? "") : FormatMode(primaryOffer.Mode);
                if (sessionDuration is null && primaryOffer is not null)
                    sessionDuration = ExtractSessionDuration(primaryOffer.Conditions);

                return new TutorSearchResultDto(
                    t.Id,
                    displayName,
                    t.Slug,
                    TeacherPublicName.GeneralLocation(t.City, null),
                    TeacherPublicName.GeneralLocation(null, t.Country),
                    blurb,
                    t.Language,
                    t.Currency,
                    minPrice,
                    maxPrice,
                    subjects,
                    modes,
                    null,
                    photo.Url,
                    0,
                    0m,
                    levels,
                    specialties,
                    languages,
                    sessionDuration,
                    t.ApprovedByExpertGroupId is not null || portfolio.IsVerified,
                    hasFlexible,
                    t.ApprovedByExpertGroupId,
                    group?.Name,
                    group?.LogoUrl,
                    group?.BannerUrl,
                    group?.PrimaryColor,
                    group?.SecondaryColor,
                    TeacherPublicPhotoResolver.ToApi(photo.Kind),
                    photo.IsGroupLogoFallback,
                    initials,
                    offerTitle,
                    string.IsNullOrWhiteSpace(offerMode) ? null : offerMode,
                    0);
            })
            .Where(r => !filters.MinRating.HasValue || (r.Rating ?? 0) >= filters.MinRating.Value)
            .Where(r =>
            {
                if (string.IsNullOrWhiteSpace(levelFilter))
                    return true;
                return MatchesLevelFilter(r.Levels ?? [], levelFilter!);
            })
            .OrderBy(r => r.Name)
            .ToList();

        return results;
    }

    public Task<IReadOnlyList<ExpertGroupSearchOptionDto>> ListActiveExpertGroupsAsync(
        CancellationToken ct = default)
    {
        IReadOnlyList<ExpertGroupSearchOptionDto> list = _db.ExpertGroups
            .Where(g => g.IsActive)
            .OrderByDescending(g => g.IsInternational)
            .ThenBy(g => g.Name)
            .Select(g => new ExpertGroupSearchOptionDto(g.Id, g.Name, g.CountryCode, g.IsInternational))
            .ToList();
        return Task.FromResult(list);
    }

    private static bool MatchesLevelFilter(IReadOnlyList<string> levels, string filter)
    {
        if (levels.Count == 0)
            return false;

        return levels.Any(l =>
            l.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || filter.Contains(l, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllLevels(string? level) =>
        string.Equals(level?.Trim(), "Tous niveaux", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractOfferingLevel(string? conditions)
    {
        var schedule = TryParseSchedule(conditions);
        return string.IsNullOrWhiteSpace(schedule?.Level) ? null : schedule.Level.Trim();
    }

    private static int? ExtractSessionDuration(string? conditions)
    {
        var schedule = TryParseSchedule(conditions);
        return schedule?.SessionDurationMin > 0 ? schedule.SessionDurationMin : null;
    }

    private static OfferingScheduleDto? TryParseSchedule(string? conditions)
    {
        if (string.IsNullOrWhiteSpace(conditions))
            return null;

        try
        {
            return JsonSerializer.Deserialize<OfferingScheduleDto>(conditions, ScheduleJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PortfolioExtras ParsePortfolioExtras(string? portfolioJson)
    {
        if (string.IsNullOrWhiteSpace(portfolioJson))
            return PortfolioExtras.Empty;

        try
        {
            using var doc = JsonDocument.Parse(portfolioJson);
            var root = doc.RootElement;
            var levels = ReadStringList(root, "levels", "Levels");
            var subjects = ReadStringList(root, "subjects", "Subjects");
            var languages = ReadStringList(root, "languages", "Languages");
            var hasDiplomas = HasCredentialItems(root, "diplomas", "Diplomas");
            var hasCerts = HasCredentialItems(root, "certifications", "Certifications");

            return new PortfolioExtras(levels, subjects, languages, hasDiplomas || hasCerts);
        }
        catch (JsonException)
        {
            return PortfolioExtras.Empty;
        }
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
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return [];
    }

    private static bool HasCredentialItems(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                continue;

            return p.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.Object
                && ((item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(t.GetString()))
                    || (item.TryGetProperty("Title", out var t2) && t2.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(t2.GetString()))));
        }

        return false;
    }

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "En personne",
        LessonMode.Online => "En ligne",
        LessonMode.Hybrid => "Hybride",
        _ => mode.ToString()
    };

    private sealed record PortfolioExtras(
        List<string> Levels,
        List<string> Subjects,
        List<string> Languages,
        bool IsVerified)
    {
        public static PortfolioExtras Empty { get; } = new([], [], [], false);
    }
}
