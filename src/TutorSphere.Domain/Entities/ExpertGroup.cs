using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Groupe d'experts éducatifs chargé de valider les fiches enseignants.
/// Aucune exclusivité territoriale : plusieurs groupes peuvent couvrir le même pays, et un groupe
/// peut n'en déclarer aucun. <see cref="CountryCode"/> n'est qu'une indication de rattachement.
/// Routage revue : pays de l'enseignant si un seul groupe le revendique, sinon le groupe désigné
/// par défaut (<see cref="IsDefaultReviewGroup"/>).
/// Administré par un <see cref="ExpertGroupManagerMandate"/> (Responsable du groupe).
/// </summary>
public class ExpertGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? Description { get; set; }

    /// <summary>Coordonnées miroir du Responsable actif (affichage rapide / rétrocompat).</summary>
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>
    /// ISO pays de rattachement (ex. CA, FR), purement indicatif : facultatif, non exclusif.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>Portée annoncée : le groupe se déclare sans frontière particulière.</summary>
    public bool IsInternational { get; set; }

    /// <summary>
    /// Groupe qui recueille les candidatures spontanées qu'aucun pays ne permet de rattacher.
    /// Un seul groupe actif peut porter ce rôle ; l'administrateur plateforme le désigne.
    /// </summary>
    public bool IsDefaultReviewGroup { get; set; }

    public bool IsActive { get; set; } = true;

    public ExpertGroupLifecycleStatus LifecycleStatus { get; set; } = ExpertGroupLifecycleStatus.Draft;

    /// <summary>Mandat Responsable principal actif (dénormalisé pour jointures rapides).</summary>
    public Guid? ActiveManagerMandateId { get; set; }

    /// <summary>Adhésion du Responsable actif (contrat produit Lot 1).</summary>
    public Guid? GroupManagerMembershipId { get; set; }

    public DateTime? ManagerAssignedAtUtc { get; set; }
    public string? ManagerAssignedByAdminId { get; set; }

    /// <summary>
    /// Processus d'approbation des enseignants. Par défaut dossier seul (la démonstration n'est pas imposée).
    /// </summary>
    public TeacherApprovalTrack TeacherApprovalTrack { get; set; } = TeacherApprovalTrack.FileOnly;

    /// <summary>
    /// Part TutorSphere prélevée sur le net (après frais Stripe/PayPal). Défaut 30 %.
    /// Le complément va au groupe (enseignants rattachés), pas à l’enseignant.
    /// </summary>
    public decimal PlatformCommissionPercent { get; set; } = 30m;

    public ICollection<ExpertGroupMember> Members { get; set; } = [];
    public ICollection<ExpertGroupManagerMandate> ManagerMandates { get; set; } = [];
    public ICollection<GroupOffer> Offers { get; set; } = [];
    public ICollection<ExpertGroupDefinedRole> DefinedRoles { get; set; } = [];
}
