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
        var query = _db.Tenants
            .Where(t => t.Status == TenantStatus.Active && t.IsPublicProfile);

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
        var tenantIds = tenants.Select(t => t.Id).ToList();

        var offeringsQuery = _db.SubscriptionOfferings
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

        if (!string.IsNullOrWhiteSpace(filters.Level))
        {
            var level = filters.Level.Trim();
            offeringsQuery = offeringsQuery.Where(o =>
                (o.Subject != null && o.Subject.Contains(level)) ||
                (o.Title != null && o.Title.Contains(level)) ||
                (o.Conditions != null && o.Conditions.Contains(level)));
        }

        var offerings = offeringsQuery.ToList();
        var offeringsByTenant = offerings
            .GroupBy(o => o.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = tenants
            .Where(t => offeringsByTenant.ContainsKey(t.Id))
            .Select(t =>
            {
                var tenantOfferings = offeringsByTenant[t.Id];
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
                    null);
            })
            .OrderBy(r => r.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<TutorSearchResultDto>>(results);
    }

    private static string FormatMode(LessonMode mode) => mode switch
    {
        LessonMode.InPerson => "En personne",
        LessonMode.Online => "En ligne",
        LessonMode.Hybrid => "Hybride",
        _ => mode.ToString()
    };
}
