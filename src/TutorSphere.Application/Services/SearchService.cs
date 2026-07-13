using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Search;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ISearchService
{
    Task<IReadOnlyList<TutorSearchResultDto>> SearchTutorsAsync(
        TutorSearchFilters filters,
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
        // Directory is cross-tenant: ignore JWT tenant scoping on offerings.
        var offeringsQuery = _db.SubscriptionOfferingsForAnyTenant
            .Where(o => o.IsActive);

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
        var tenantIdsWithOffers = offerings.Select(o => o.TenantId).Distinct().ToList();
        if (tenantIdsWithOffers.Count == 0)
            return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>([]);

        var query = _db.Tenants
            .Where(t => t.Status == TenantStatus.Active
                        && t.IsPublicProfile
                        && tenantIdsWithOffers.Contains(t.Id));

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
        var offeringsByTenant = offerings
            .GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var brandings = _db.TenantBrandings
            .Where(b => tenantIds.Contains(b.TenantId))
            .Select(b => new { b.TenantId, b.LogoUrl, b.Portfolio })
            .ToList();

        var logosByTenant = brandings
            .Where(b => !string.IsNullOrWhiteSpace(b.LogoUrl))
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.First().LogoUrl!.Trim());

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

        var results = tenants
            .Where(t => offeringsByTenant.ContainsKey(t.Id))
            .Select(t =>
            {
                var tenantOfferings = offeringsByTenant[t.Id];
                studentCounts.TryGetValue(t.Id, out var studentCount);
                weeklyHoursByTenant.TryGetValue(t.Id, out var weeklyHours);
                logosByTenant.TryGetValue(t.Id, out var photoUrl);
                portfolioByTenant.TryGetValue(t.Id, out var portfolio);
                portfolio ??= PortfolioExtras.Empty;

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

                return new TutorSearchResultDto(
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.City,
                    t.Country,
                    t.Description,
                    t.Language,
                    t.Currency,
                    tenantOfferings.Min(o => o.Price),
                    tenantOfferings.Max(o => o.Price),
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
                    hasFlexible);
            })
            .Where(r => !filters.MinRating.HasValue || (r.Rating ?? 0) >= filters.MinRating.Value)
            .Where(r => string.IsNullOrWhiteSpace(levelFilter)
                        || MatchesLevelFilter(r.Levels ?? [], levelFilter!))
            .OrderBy(r => r.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>(results);
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
