using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Plage de disponibilité hebdomadaire d’un enseignant (tenant).
/// Plusieurs lignes peuvent exister pour le même jour (ex. 08:00–12:00 et 14:00–18:00).
/// Les créneaux de réservation se calculent ensuite à partir de la durée de séance.
/// </summary>
public class TeacherAvailability : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>.NET <see cref="DayOfWeek"/> (dimanche = 0, lundi = 1, …).</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
}
