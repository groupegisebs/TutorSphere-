using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Rôle métier défini pour un groupe d'experts (catalogue intégré persisté, ou rôle personnalisé).
/// Distinct du mandat « Responsable du groupe » (Identity / Manager).
/// </summary>
public class ExpertGroupDefinedRole : BaseEntity
{
    public Guid ExpertGroupId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BadgeColor { get; set; } = "#2563EB";
    public string? PermissionsJson { get; set; }

    /// <summary>Clé catalogue (expert, pedagogy, …) ; null si rôle personnalisé.</summary>
    public string? SystemKey { get; set; }

    public bool SuperAdminOnly { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    public ExpertGroup ExpertGroup { get; set; } = null!;
}
