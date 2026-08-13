using System.Text.Json;
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

    public SearchService(IApplicationDbContext db) => _db = db;

    public Task<IReadOnlyList<TutorSearchResultDto>> SearchTutorsAsync(
        TutorSearchFilters filters,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Annuaire = fiches publiques éligibles (pas seulement celles qui ont déjà une offre).
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
            return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>([]);

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

        // Filtres liés aux offres : ne garder que les enseignants qui matchent.
        var offerFilterActive = !string.IsNullOrWhiteSpace(filters.Subject)
            || filters.MinPrice.HasValue
            || filters.MaxPrice.HasValue
            || filters.Mode.HasValue;

        if (offerFilterActive)
        {
            tenants = tenants.Where(t => offeringsByTenant.ContainsKey(t.Id)).ToList();
            if (tenants.Count == 0)
                return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>([]);
            tenantIds = tenants.Select(t => t.Id).ToList();
        }

        var groupIds = tenants
            .Where(t => t.ApprovedByExpertGroupId.HasValue)
            .Select(t => t.ApprovedByExpertGroupId!.Value)
            .Distinct()
            .ToList();
        var groupNames = _db.ExpertGroups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToList()
            .ToDictionary(g => g.Id, g => g.Name);

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

        var studentCounts = _db.StudentsForAnyTenant
            .Where(s => tenantIds.Contains(s.TenantId) && s.IsActive)
            .GroupBy(s => s.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.TenantId, x => x.Count);

        var (weekStart, weekEnd) = GetUtcWeekBounds(DateTime.UtcNow);
        var weekLessons = _db.LessonsForAnyTenant
            .Where(l => tenantIds.Contains(l.TenantId)
                        && l.SettlementStatus != LessonSettlementStatus.CancelledFree
                        && l.StartTime >= weekStart
                        && l.StartTime < weekEnd)
            .Select(l => new { l.TenantId, l.StartTime, l.EndTime })
            .ToList();

        var weeklyHoursByTenant = weekLessons
            .GroupBy(l => l.TenantId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(
                    (decimal)g.Sum(l => (l.EndTime - l.StartTime).TotalHours),
                    1,
                    MidpointRounding.AwayFromZero));

        var levelFilter = filters.Level?.Trim();
        var subjectFilter = filters.Subject?.Trim();

        var results = tenants
            .Select(t =>
            {
                offeringsByTenant.TryGetValue(t.Id, out var tenantOfferings);
                tenantOfferings ??= [];

                studentCounts.TryGetValue(t.Id, out var studentCount);
                weeklyHoursByTenant.TryGetValue(t.Id, out var weeklyHours);
                logosByTenant.TryGetValue(t.Id, out var photoUrl);
                portfolioByTenant.TryGetValue(t.Id, out var portfolio);
                portfolio ??= PortfolioExtras.Empty;
                presentationByTenant.TryGetValue(t.Id, out var presentation);

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

                var blurb = !string.IsNullOrWhiteSpace(t.Description)
                    ? t.Description
                    : presentation;

                decimal? minPrice = tenantOfferings.Count > 0 ? tenantOfferings.Min(o => o.Price) : null;
                decimal? maxPrice = tenantOfferings.Count > 0 ? tenantOfferings.Max(o => o.Price) : null;

                return new TutorSearchResultDto(
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.City,
                    t.Country,
                    blurb,
                    t.Language,
                    t.Currency,
                    minPrice,
                    maxPrice,
                    subjects,
                    modes,
                    null,
                    string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl,
                    studentCount,
                    weeklyHours,
                    levels,
                    specialties,
                    languages,
                    sessionDuration,
                    portfolio.IsVerified,
                    hasFlexible,
                    t.ApprovedByExpertGroupId,
                    t.ApprovedByExpertGroupId is Guid gid && groupNames.TryGetValue(gid, out var gn) ? gn : null);
            })
            .Where(r => !filters.MinRating.HasValue || (r.Rating ?? 0) >= filters.MinRating.Value)
            .Where(r =>
            {
                if (string.IsNullOrWhiteSpace(levelFilter))
                    return true;
                // Niveau renseigné : match ; sinon exclure seulement si le filtre est actif.
                return MatchesLevelFilter(r.Levels ?? [], levelFilter!);
            })
            .OrderBy(r => r.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>(results);
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

    /// <summary>Semaine calendaire lundi → dimanche (UTC).</summary>
    private static (DateTime Start, DateTime End) GetUtcWeekBounds(DateTime utcNow)
    {
        var today = utcNow.Date;
        var offset = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var weekStart = today.AddDays(-offset);
        return (weekStart, weekStart.AddDays(7));
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
