using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Groupe d'experts éducatifs chargé de valider les fiches enseignants.
/// Règle produit : au plus un groupe par code pays, et exactement au plus un groupe international
/// (<see cref="IsInternational"/> = true, <see cref="CountryCode"/> null).
/// Routage revue : pays de l'enseignant → groupe pays ; sinon groupe international.
/// </summary>
public class ExpertGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>ISO pays (ex. CA, FR). Null si groupe international.</summary>
    public string? CountryCode { get; set; }

    /// <summary>True = groupe international unique (CountryCode doit être null).</summary>
    public bool IsInternational { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ExpertGroupMember> Members { get; set; } = [];
}
