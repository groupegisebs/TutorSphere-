using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Search;
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

        if (!string.IsNullOrWhiteSpace(filters.Level))
        {
            var level = filters.Level.Trim();
            offeringsQuery = offeringsQuery.Where(o =>
                (o.Subject != null && o.Subject.Contains(level)) ||
                (o.Title != null && o.Title.Contains(level)) ||
                (o.Conditions != null && o.Conditions.Contains(level)));
        }

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

        var logosByTenant = _db.TenantBrandings
            .Where(b => tenantIds.Contains(b.TenantId) && b.LogoUrl != null && b.LogoUrl != "")
            .Select(b => new { b.TenantId, b.LogoUrl })
            .ToList()
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.First().LogoUrl!.Trim());

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

        var results = tenants
            .Where(t => offeringsByTenant.ContainsKey(t.Id))
            .Select(t =>
            {
                var tenantOfferings = offeringsByTenant[t.Id];
                studentCounts.TryGetValue(t.Id, out var studentCount);
                weeklyHoursByTenant.TryGetValue(t.Id, out var weeklyHours);
                logosByTenant.TryGetValue(t.Id, out var photoUrl);

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
                    tenantOfferings
                        .Select(o => o.Subject)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Cast<string>()
                        .ToList(),
                    tenantOfferings
                        .Select(o => FormatMode(o.Mode))
                        .Distinct()
                        .ToList(),
                    null,
                    string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl,
                    studentCount,
                    weeklyHours);
            })
            .Where(r => !filters.MinRating.HasValue || (r.Rating ?? 0) >= filters.MinRating.Value)
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

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "En personne",
        LessonMode.Online => "En ligne",
        LessonMode.Hybrid => "Hybride",
        _ => mode.ToString()
    };
}
