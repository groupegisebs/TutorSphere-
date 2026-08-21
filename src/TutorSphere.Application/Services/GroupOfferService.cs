using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IGroupOfferService
{
    Task<GroupOffersCatalogDto?> GetCatalogAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupOfferListItemDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupOfferListItemDto> CreateDraftAsync(
        Guid groupId,
        string userId,
        CreateGroupOfferRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
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

    Task<IReadOnlyList<GroupOfferAssignableTeacherDto>> ListAssignableTeachersAsync(
        Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupOfferTeacherAssignmentDto>> ListAssignmentsAsync(
        Guid offerId, CancellationToken ct = default);
    Task<GroupOfferTeacherAssignmentDto> AssignTeacherAsync(
        Guid offerId,
        string managerUserId,
        AssignGroupOfferTeacherRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
    Task UnassignTeacherAsync(
        Guid offerId,
        Guid teacherTenantId,
        string managerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null);
}

public class GroupOfferService(
    IApplicationDbContext db,
    IExpertGroupManagerService managers,
    ISubscriptionOfferingService offerings,
    IExpertGovernanceAuditService audit) : IGroupOfferService
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
        Guid groupId,
        string userId,
        CreateGroupOfferRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Le nom de l'offre est requis.");

        var allowedAsPlatform = asPlatformAdmin && actAsGroupId is Guid gid && gid == groupId;
        if (!allowedAsPlatform && !managers.IsActiveManager(userId, groupId))
            throw new InvalidOperationException(
                "Seul le Responsable du groupe (ou un admin plateforme en mode suppléant) peut créer une offre.");

        var scope = ResolveScope(group, request.IsInternational, request.MarketCountryCode, request.MarketCountryCodes);
        var (storedCycle, levelsCsv) = ResolveSchooling(request.SchoolCycle, request.Levels);

        var offer = new GroupOffer
        {
            ExpertGroupId = groupId,
            DisciplineId = request.DisciplineId,
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim(),
            SchoolCycle = storedCycle,
            LevelsCsv = levelsCsv,
            PricingModel = request.PricingModel,
            Currency = scope.Currency,
            FixedPrice = request.FixedPrice ?? request.RecommendedPrice,
            MinimumPrice = request.MinimumPrice,
            RecommendedPrice = request.RecommendedPrice ?? request.FixedPrice,
            MaximumPrice = request.MaximumPrice,
            IsInternational = scope.IsInternational,
            MarketCountryCode = scope.MarketCountryCode,
            VisibleCountryCodes = GroupOfferCurrencyRules.ToCountryCsv(scope.MarketCountryCodes),
            Status = GroupOfferStatus.Draft,
            CreatedByUserId = userId
        };
        db.Add(offer);
        await db.SaveChangesAsync(ct);

        try
        {
            await audit.RecordAsync(
                ExpertGovernanceEventType.GroupOfferCreated,
                userId,
                $"Offre brouillon « {offer.Name} »",
                groupId,
                relatedEntityId: offer.Id,
                isNotification: false,
                ct: ct);
        }
        catch
        {
            // L'offre est déjà persistée — ne pas faire échouer la création pour l'audit.
        }

        return ToDto(offer, 0);
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
        if (offer.Status is GroupOfferStatus.Suspended)
            throw new InvalidOperationException("Une offre suspendue ne peut pas être modifiée. Republiez-la ou créez une nouvelle offre.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Le nom de l'offre est requis.");

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == offer.ExpertGroupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");

        var scope = ResolveScope(group, request.IsInternational, request.MarketCountryCode, request.MarketCountryCodes);
        var (storedCycle, levelsCsv) = ResolveSchooling(request.SchoolCycle, request.Levels);

        offer.Name = request.Name.Trim();
        offer.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        offer.ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim();
        offer.DisciplineId = request.DisciplineId;
        offer.SchoolCycle = storedCycle;
        offer.LevelsCsv = levelsCsv;
        offer.PricingModel = request.PricingModel;
        offer.Currency = scope.Currency;
        offer.FixedPrice = request.FixedPrice ?? request.RecommendedPrice;
        offer.MinimumPrice = request.MinimumPrice;
        offer.RecommendedPrice = request.RecommendedPrice ?? request.FixedPrice;
        offer.MaximumPrice = request.MaximumPrice;
        offer.IsInternational = scope.IsInternational;
        offer.MarketCountryCode = scope.MarketCountryCode;
        offer.VisibleCountryCodes = GroupOfferCurrencyRules.ToCountryCsv(scope.MarketCountryCodes);
        offer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(offer, CountAssignments(offer.Id));
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

        await audit.RecordAsync(
            ExpertGovernanceEventType.GroupOfferPublished,
            managerUserId,
            $"Offre publiée « {offer.Name} »",
            offer.ExpertGroupId,
            relatedEntityId: offer.Id,
            isNotification: false,
            ct: ct);
    }

    public Task<IReadOnlyList<GroupOfferAssignableTeacherDto>> ListAssignableTeachersAsync(
        Guid groupId, CancellationToken ct = default)
    {
        IReadOnlyList<GroupOfferAssignableTeacherDto> list = db.Tenants
            .Where(t => t.ApprovedByExpertGroupId == groupId
                        && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .AsEnumerable()
            .Select(t => new GroupOfferAssignableTeacherDto(
                t.Id,
                t.Name,
                null,
                t.City,
                t.Country))
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<GroupOfferTeacherAssignmentDto>> ListAssignmentsAsync(
        Guid offerId, CancellationToken ct = default)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        var rows = db.GroupOfferTeachers
            .Where(a => a.GroupOfferId == offerId
                        && a.AssignmentStatus != GroupOfferTeacherAssignmentStatus.Removed)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var tenantIds = rows.Select(r => r.TeacherTenantId).Distinct().ToList();
        var tenants = db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);

        IReadOnlyList<GroupOfferTeacherAssignmentDto> list = rows.Select(r => new GroupOfferTeacherAssignmentDto(
            r.Id,
            r.GroupOfferId,
            r.TeacherTenantId,
            tenants.GetValueOrDefault(r.TeacherTenantId) ?? r.TeacherTenantId.ToString(),
            r.AssignmentStatus,
            r.TeacherPrice,
            r.SubscriptionOfferingId,
            r.CreatedAt)).ToList();

        return Task.FromResult(list);
    }

    public async Task<GroupOfferTeacherAssignmentDto> AssignTeacherAsync(
        Guid offerId,
        string managerUserId,
        AssignGroupOfferTeacherRequest request,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        EnsureCanManage(offer, managerUserId, asPlatformAdmin, actAsGroupId);

        if (offer.Status is GroupOfferStatus.Archived or GroupOfferStatus.Suspended)
            throw new InvalidOperationException("Cette offre ne peut plus être affectée.");

        var tenant = db.Tenants.FirstOrDefault(t => t.Id == request.TeacherTenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");

        if (tenant.ApprovedByExpertGroupId != offer.ExpertGroupId
            || tenant.ExpertApprovalStatus != ExpertApprovalStatus.Approved)
            throw new InvalidOperationException(
                "Seuls les enseignants approuvés du groupe peuvent recevoir cette offre.");

        var existing = db.GroupOfferTeachers.FirstOrDefault(a =>
            a.GroupOfferId == offerId && a.TeacherTenantId == request.TeacherTenantId);

        if (existing is not null
            && existing.AssignmentStatus is not (GroupOfferTeacherAssignmentStatus.Removed
                or GroupOfferTeacherAssignmentStatus.Declined
                or GroupOfferTeacherAssignmentStatus.Suspended))
            throw new InvalidOperationException("Cet enseignant est déjà affecté à cette offre.");

        var price = request.TeacherPrice
            ?? offer.RecommendedPrice
            ?? offer.FixedPrice
            ?? 0m;

        Guid offeringId;
        if (request.SubscriptionOfferingId is Guid existingOfferingId)
        {
            var existingOffering = db.SubscriptionOfferingsForAnyTenant
                .FirstOrDefault(o => o.Id == existingOfferingId && o.TenantId == tenant.Id)
                ?? throw new InvalidOperationException("Offre enseignant introuvable pour ce compte.");
            existingOffering.Title = offer.Name;
            existingOffering.Description = offer.ShortDescription ?? offer.FullDescription ?? existingOffering.Description;
            existingOffering.Subject = string.IsNullOrWhiteSpace(existingOffering.Subject) ? offer.Name : existingOffering.Subject;
            existingOffering.Price = price;
            existingOffering.Currency = offer.Currency;
            existingOffering.UpdatedAt = DateTime.UtcNow;
            offeringId = existingOffering.Id;
        }
        else
        {
            var created = await offerings.CreateForTenantAsync(
                tenant.Id,
                new CreateSubscriptionOfferingRequest(
                    Title: offer.Name,
                    Description: offer.ShortDescription ?? offer.FullDescription,
                    Subject: offer.Name,
                    Price: price,
                    Currency: offer.Currency,
                    DurationDays: 30,
                    SessionCount: 4,
                    Frequency: null,
                    Mode: "En ligne",
                    Conditions: null,
                    Schedule: new OfferingScheduleDto(
                        "mois",
                        "weekly",
                        60,
                        null,
                        null,
                        Array.Empty<OfferingScheduleSlotDto>(),
                        BillingMode: "hourly"),
                    MaxCapacity: request.Capacity is > 0 ? request.Capacity.Value : 20),
                ct);
            offeringId = created.Id;
        }

        GroupOfferTeacher assignment;
        if (existing is not null)
        {
            assignment = existing;
            assignment.AssignmentStatus = GroupOfferTeacherAssignmentStatus.Active;
            assignment.TeacherPrice = price;
            assignment.Capacity = request.Capacity;
            assignment.AvailableFrom = DateTime.UtcNow;
            assignment.ApprovedByUserId = managerUserId;
            assignment.SubscriptionOfferingId = offeringId;
            assignment.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            assignment = new GroupOfferTeacher
            {
                GroupOfferId = offerId,
                TeacherTenantId = tenant.Id,
                AssignmentStatus = GroupOfferTeacherAssignmentStatus.Active,
                TeacherPrice = price,
                Capacity = request.Capacity,
                AvailableFrom = DateTime.UtcNow,
                ApprovedByUserId = managerUserId,
                SubscriptionOfferingId = offeringId
            };
            db.Add(assignment);
        }

        // Publier le catalogue groupe si encore brouillon (affectation = mise en circulation).
        if (offer.Status is GroupOfferStatus.Draft or GroupOfferStatus.UnderReview or GroupOfferStatus.Approved)
        {
            offer.Status = GroupOfferStatus.Published;
            offer.ApprovedByManagerUserId = managerUserId;
            offer.PublishedAtUtc = DateTime.UtcNow;
            offer.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return new GroupOfferTeacherAssignmentDto(
            assignment.Id,
            assignment.GroupOfferId,
            assignment.TeacherTenantId,
            tenant.Name,
            assignment.AssignmentStatus,
            assignment.TeacherPrice,
            assignment.SubscriptionOfferingId,
            assignment.CreatedAt);
    }

    public async Task UnassignTeacherAsync(
        Guid offerId,
        Guid teacherTenantId,
        string managerUserId,
        CancellationToken ct = default,
        bool asPlatformAdmin = false,
        Guid? actAsGroupId = null)
    {
        var offer = db.GroupOffers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new InvalidOperationException("Offre introuvable.");

        EnsureCanManage(offer, managerUserId, asPlatformAdmin, actAsGroupId);

        var assignment = db.GroupOfferTeachers.FirstOrDefault(a =>
            a.GroupOfferId == offerId && a.TeacherTenantId == teacherTenantId)
            ?? throw new InvalidOperationException("Affectation introuvable.");

        assignment.AssignmentStatus = GroupOfferTeacherAssignmentStatus.Removed;
        assignment.UpdatedAt = DateTime.UtcNow;

        if (assignment.SubscriptionOfferingId is Guid offeringId)
        {
            try
            {
                await offerings.DeactivateAsync(offeringId, ct);
            }
            catch
            {
                // L'affectation est retirée même si la désactivation catalogue échoue.
            }
        }

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

    private sealed record OfferScope(
        bool IsInternational,
        string? MarketCountryCode,
        IReadOnlyList<string> MarketCountryCodes,
        string Currency);

    /// <summary>
    /// Portée et devise d'une offre. Une offre locale ne vise que le pays du groupe ; une offre
    /// internationale vise une liste de pays, dont la devise commune fait la devise de l'offre —
    /// USD dès que ces devises diffèrent, faute de taux de change dans l'application.
    /// </summary>
    private static OfferScope ResolveScope(
        ExpertGroup group,
        bool isInternational,
        string? marketCountryCode,
        IReadOnlyList<string>? marketCountryCodes)
    {
        if (isInternational)
        {
            // Le champ au singulier reste accepté : les appelants qui n'envoient qu'un pays
            // (formulaire d'ajout d'enseignant) doivent continuer de fonctionner.
            var codes = GroupOfferCurrencyRules.NormalizeCountryCodes(
                (marketCountryCodes ?? []).Concat([marketCountryCode]));
            if (codes.Count == 0)
                throw new InvalidOperationException(
                    "Une offre internationale nécessite au moins un pays où elle est valable.");

            return new OfferScope(
                true,
                codes[0],
                codes,
                GroupOfferCurrencyRules.ResolveCurrencyForCountries(codes));
        }

        // Le groupe n'a plus forcément de pays : sans rattachement, « locale » ne désigne aucun
        // marché et la devise serait arbitraire. L'offre doit alors nommer ses pays elle-même.
        var localCountry = GroupOfferCurrencyRules.NormalizeCountryCode(group.CountryCode);
        if (string.IsNullOrEmpty(localCountry))
            throw new InvalidOperationException(
                "Ce groupe ne déclare aucun pays de rattachement : choisissez « Internationale » "
                + "et indiquez les pays où l'offre est valable, ou renseignez le pays du groupe.");

        return new OfferScope(
            false,
            string.IsNullOrEmpty(localCountry) ? null : localCountry,
            string.IsNullOrEmpty(localCountry) ? [] : [localCountry],
            GroupOfferCurrencyRules.ResolveCurrency(localCountry));
    }

    /// <summary>
    /// Cycle et niveaux retenus. Les niveaux étrangers au cycle sont écartés ici plutôt qu'affichés
    /// plus tard : « Université » sur un cycle primaire ne veut rien dire pour un parent.
    /// </summary>
    private static (string? StoredCycle, string? LevelsCsv) ResolveSchooling(
        SchoolCycle? cycle,
        IReadOnlyList<string>? levels)
    {
        var kept = SchoolLevelCatalog.LevelsWithinCycle(levels, cycle);
        return (SchoolLevelCatalog.ToStoredCycle(cycle), SchoolLevelCatalog.ToLevelCsv(kept));
    }

    private int CountAssignments(Guid offerId) =>
        db.GroupOfferTeachers.Count(a =>
            a.GroupOfferId == offerId
            && a.AssignmentStatus != GroupOfferTeacherAssignmentStatus.Removed
            && a.AssignmentStatus != GroupOfferTeacherAssignmentStatus.Declined);

    private IReadOnlyList<GroupOfferListItemDto> MapList(Guid groupId)
    {
        var offers = db.GroupOffers
            .Where(o => o.ExpertGroupId == groupId)
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .ToList();

        if (offers.Count == 0)
            return Array.Empty<GroupOfferListItemDto>();

        var offerIds = offers.Select(o => o.Id).ToList();
        var assignmentRows = db.GroupOfferTeachers
            .Where(a => offerIds.Contains(a.GroupOfferId)
                        && a.AssignmentStatus != GroupOfferTeacherAssignmentStatus.Removed
                        && a.AssignmentStatus != GroupOfferTeacherAssignmentStatus.Declined)
            .Select(a => a.GroupOfferId)
            .ToList();

        var counts = assignmentRows
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return offers
            .Select(o => ToDto(o, counts.GetValueOrDefault(o.Id)))
            .ToList();
    }

    private static GroupOfferListItemDto ToDto(GroupOffer o, int assignedCount)
    {
        // Les offres créées avant la sélection multi-pays n'ont que le pays au singulier : on le
        // reprend, sinon leur ligne s'afficherait sans aucun pays.
        var countries = GroupOfferCurrencyRules.ParseCountryCsv(o.VisibleCountryCodes);
        if (countries.Count == 0 && !string.IsNullOrWhiteSpace(o.MarketCountryCode))
            countries = GroupOfferCurrencyRules.NormalizeCountryCodes([o.MarketCountryCode]);

        return new(
            o.Id, o.ExpertGroupId, o.Name, o.Code, o.Status, o.PricingModel,
            o.Currency, o.RecommendedPrice ?? o.FixedPrice, o.CreatedAt, o.PublishedAtUtc,
            o.ShortDescription, o.IsInternational, o.MarketCountryCode, assignedCount,
            countries,
            SchoolLevelCatalog.ParseCycle(o.SchoolCycle),
            SchoolLevelCatalog.ParseLevelCsv(o.LevelsCsv));
    }
}
