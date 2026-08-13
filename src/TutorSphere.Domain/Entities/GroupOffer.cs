using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Offre pédagogique commune définie par un groupe d'experts.</summary>
public class GroupOffer : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public Guid? DisciplineId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public string? ImageUrl { get; set; }

    public string? SchoolCycle { get; set; }
    public string? LevelsCsv { get; set; }
    public string? LanguagesCsv { get; set; }
    public string? VisibleCountryCodes { get; set; }

    public GroupOfferPricingModel PricingModel { get; set; } = GroupOfferPricingModel.Fixed;
    public string Currency { get; set; } = "XAF";
    public decimal? FixedPrice { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? RecommendedPrice { get; set; }
    public decimal? MaximumPrice { get; set; }

    /// <summary>False = offre locale (pays du groupe) ; true = offre internationale (marché cible).</summary>
    public bool IsInternational { get; set; }

    /// <summary>Pays de marché (ISO). Local = pays du groupe ; International = pays cible pour la devise.</summary>
    public string? MarketCountryCode { get; set; }

    public GroupOfferStatus Status { get; set; } = GroupOfferStatus.Draft;

    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ApprovedByManagerUserId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public Discipline? Discipline { get; set; }
    public ICollection<GroupOfferTeacher> Teachers { get; set; } = [];
}
