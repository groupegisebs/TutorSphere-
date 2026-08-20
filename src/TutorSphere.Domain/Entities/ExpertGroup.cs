using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Groupe d'experts éducatifs chargé de valider les fiches enseignants.
/// Règle produit : au plus un groupe <em>actif</em> par code pays, et au plus un groupe
/// international actif (<see cref="IsInternational"/> = true, <see cref="CountryCode"/> null).
/// Un groupe inactif (brouillon, suspendu, archivé) n’occupe pas le créneau.
/// Routage revue : pays de l'enseignant → groupe pays ; sinon groupe international.
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

    /// <summary>ISO pays (ex. CA, FR). Null si groupe international.</summary>
    public string? CountryCode { get; set; }

    /// <summary>True = groupe international unique (CountryCode doit être null).</summary>
    public bool IsInternational { get; set; }

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

    public ICollection<ExpertGroupMember> Members { get; set; } = [];
    public ICollection<ExpertGroupManagerMandate> ManagerMandates { get; set; } = [];
    public ICollection<GroupOffer> Offers { get; set; } = [];
    public ICollection<ExpertGroupDefinedRole> DefinedRoles { get; set; } = [];
}
