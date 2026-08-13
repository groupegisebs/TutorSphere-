using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IGroupOfferService
{
    Task<GroupOffersCatalogDto?> GetCatalogAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupOfferListItemDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupOfferListItemDto> CreateDraftAsync(Guid groupId, string userId, CreateGroupOfferRequest request, CancellationToken ct = default);
    Task<GroupOfferListItemDto> UpdateDraftAsync(
        Guid offerId,
        string userId,
        UpdateGroupOfferRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task DeleteAsync(
        Guid offerId,
        string userId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task PublishAsync(
        Guid offerId,
        string managerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
}

public class GroupOfferService(IApplicationDbContext db, IExpertGroupManagerService managers) : IGroupOfferService
{
    public Task<GroupOffersCatalogDto?> GetCatalogAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return Task.FromResult<GroupOffersCatalogDto?>(null);

        var groupCurrency = GroupOfferCurrencyRules.ResolveCurrency(group.CountryCode);
        var offers = MapList(groupId);
        return Task.FromResult<GroupOffersCatalogDto?>(new GroupOffersCatalogDto(
            group.Id,
            group.Name,
            group.CountryCode,
            groupCurrency,
            group.IsInternational,
            offers));
    }

    public Task<IReadOnlyList<GroupOfferListItemDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
        => Task.FromResult(MapList(groupId));

    public async Task<GroupOfferListItemDto> CreateDraftAsync(
        Guid groupId, string userId, CreateGroupOfferRequest request, CancellationToken ct = default)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Le nom de l'offre est requis.");

        var (isInternational, marketCode, currency) = ResolveScope(group, request.IsInternational, request.MarketCountryCode);

        var offer = new GroupOffer
        {
            ExpertGroupId = groupId,
            DisciplineId = request.DisciplineId,
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim(),
            PricingModel = request.PricingModel,
            Currency = currency,
            FixedPrice = request.FixedPrice ?? request.RecommendedPrice,
            MinimumPrice = request.MinimumPrice,
            RecommendedPrice = request.RecommendedPrice ?? request.FixedPrice,
            MaximumPrice = request.MaximumPrice,
            IsInternational = isInternational,
            MarketCountryCode = marketCode,
            Status = GroupOfferStatus.Draft,
            CreatedByUserId = userId
        };
        db.Add(offer);
        await db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task<GroupOfferListItemDto> UpdateDraftAsync(
        Guid offerId,
        string userId,
        UpdateGroupOfferRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        EnsureCanManage(offer, userId, asPlatformAdmin, actAsGroupId);

        if (offer.Status is GroupOfferStatus.Archived)
            throw new InvalidOperationException("Une offre archivée ne peut pas être modifiée.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Le nom de l'offre est requis.");

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == offer.ExpertGroupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");

        var (isInternational, marketCode, currency) = ResolveScope(group, request.IsInternational, request.MarketCountryCode);

        offer.Name = request.Name.Trim();
        offer.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        offer.ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim();
        offer.DisciplineId = request.DisciplineId;
        offer.PricingModel = request.PricingModel;
        offer.Currency = currency;
        offer.FixedPrice = request.FixedPrice ?? request.RecommendedPrice;
        offer.MinimumPrice = request.MinimumPrice;
        offer.RecommendedPrice = request.RecommendedPrice ?? request.FixedPrice;
        offer.MaximumPrice = request.MaximumPrice;
        offer.IsInternational = isInternational;
        offer.MarketCountryCode = marketCode;
        offer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(offer);
    }

    public async Task DeleteAsync(
        Guid offerId,
        string userId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        EnsureCanManage(offer, userId, asPlatformAdmin, actAsGroupId);

        db.Remove(offer);
        await db.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(
        Guid offerId,
        string managerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        EnsureCanManage(offer, managerUserId, asPlatformAdmin, actAsGroupId);

        if (offer.Status is not (GroupOfferStatus.Draft or GroupOfferStatus.Approved or GroupOfferStatus.UnderReview))
            throw new InvalidOperationException("Cette offre ne peut pas être publiée dans son état actuel.");

        offer.Status = GroupOfferStatus.Published;
        offer.ApprovedByManagerUserId = managerUserId;
        offer.PublishedAtUtc = DateTime.UtcNow;
        offer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private void EnsureCanManage(GroupOffer offer, string userId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        var allowedAsPlatform = asPlatformAdmin
            && actAsGroupId is Guid gid
            && gid == offer.ExpertGroupId;

        if (!allowedAsPlatform && !managers.IsActiveManager(userId, offer.ExpertGroupId))
            throw new InvalidOperationException(
                "Seul le Responsable du groupe (ou un admin plateforme en mode suppléant) peut gérer cette offre.");
    }

    private static (bool IsInternational, string? MarketCountryCode, string Currency) ResolveScope(
        ExpertGroup group,
        bool isInternational,
        string? marketCountryCode)
    {
        if (isInternational)
        {
            var market = GroupOfferCurrencyRules.NormalizeCountryCode(marketCountryCode);
            if (string.IsNullOrEmpty(market))
                throw new InvalidOperationException(
                    "Une offre internationale nécessite un pays de marché (Europe, Canada, USA, Cameroun, Afrique…).");
            return (true, market, GroupOfferCurrencyRules.ResolveCurrency(market));
        }

        var localCountry = GroupOfferCurrencyRules.NormalizeCountryCode(group.CountryCode);
        if (string.IsNullOrEmpty(localCountry) && group.IsInternational)
            throw new InvalidOperationException(
                "Pour une offre locale sur un groupe international, choisissez plutôt « Internationale » avec un pays de marché.");

        return (false, string.IsNullOrEmpty(localCountry) ? null : localCountry,
            GroupOfferCurrencyRules.ResolveCurrency(localCountry));
    }

    private IReadOnlyList<GroupOfferListItemDto> MapList(Guid groupId)
        => db.GroupOffers
            .Where(o => o.ExpertGroupId == groupId)
            .OrderByDescending(o => o.UpdatedAt)
            .AsEnumerable()
            .Select(ToDto)
            .ToList();

    private static GroupOfferListItemDto ToDto(GroupOffer o) => new(
        o.Id, o.ExpertGroupId, o.Name, o.Code, o.Status, o.PricingModel,
        o.Currency, o.RecommendedPrice ?? o.FixedPrice, o.CreatedAt, o.PublishedAtUtc,
        o.ShortDescription, o.IsInternational, o.MarketCountryCode);
}
