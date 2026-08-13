using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IGroupOfferService
{
    Task<IReadOnlyList<GroupOfferListItemDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupOfferListItemDto> CreateDraftAsync(Guid groupId, string userId, CreateGroupOfferRequest request, CancellationToken ct = default);
    Task PublishAsync(
        Guid offerId,
        string managerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
}

public class GroupOfferService(IApplicationDbContext db, IExpertGroupManagerService managers) : IGroupOfferService
{
    public Task<IReadOnlyList<GroupOfferListItemDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        IReadOnlyList<GroupOfferListItemDto> list = db.GroupOffers
            .Where(o => o.ExpertGroupId == groupId)
            .OrderByDescending(o => o.UpdatedAt)
            .Select(o => new GroupOfferListItemDto(
                o.Id, o.ExpertGroupId, o.Name, o.Code, o.Status, o.PricingModel,
                o.Currency, o.RecommendedPrice ?? o.FixedPrice, o.CreatedAt, o.PublishedAtUtc))
            .ToList();
        return Task.FromResult(list);
    }

    public async Task<GroupOfferListItemDto> CreateDraftAsync(
        Guid groupId, string userId, CreateGroupOfferRequest request, CancellationToken ct = default)
    {
        if (!db.ExpertGroups.Any(g => g.Id == groupId))
            throw new InvalidOperationException("Groupe introuvable.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Le nom de l'offre est requis.");

        var offer = new GroupOffer
        {
            ExpertGroupId = groupId,
            DisciplineId = request.DisciplineId,
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim(),
            PricingModel = request.PricingModel,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "XAF" : request.Currency.Trim().ToUpperInvariant(),
            FixedPrice = request.FixedPrice,
            MinimumPrice = request.MinimumPrice,
            RecommendedPrice = request.RecommendedPrice,
            MaximumPrice = request.MaximumPrice,
            Status = GroupOfferStatus.Draft,
            CreatedByUserId = userId
        };
        db.Add(offer);
        await db.SaveChangesAsync(ct);
        return new GroupOfferListItemDto(
            offer.Id, offer.ExpertGroupId, offer.Name, offer.Code, offer.Status, offer.PricingModel,
            offer.Currency, offer.RecommendedPrice ?? offer.FixedPrice, offer.CreatedAt, offer.PublishedAtUtc);
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

        var allowedAsPlatform = asPlatformAdmin
            && actAsGroupId is Guid gid
            && gid == offer.ExpertGroupId;

        if (!allowedAsPlatform && !managers.IsActiveManager(managerUserId, offer.ExpertGroupId))
            throw new InvalidOperationException(
                "Seul le Responsable du groupe (ou un admin plateforme en mode suppléant) peut publier une offre.");

        if (offer.Status is not (GroupOfferStatus.Draft or GroupOfferStatus.Approved or GroupOfferStatus.UnderReview))
            throw new InvalidOperationException("Cette offre ne peut pas être publiée dans son état actuel.");

        offer.Status = GroupOfferStatus.Published;
        offer.ApprovedByManagerUserId = managerUserId;
        offer.PublishedAtUtc = DateTime.UtcNow;
        offer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
