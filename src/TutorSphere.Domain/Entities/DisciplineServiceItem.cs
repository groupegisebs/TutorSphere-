using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Service détaillé fourni dans le cadre d'une discipline (ex. soutien aux devoirs,
/// préparation aux examens, suivi personnalisé), défini par le groupe d'experts responsable.
/// </summary>
public class DisciplineServiceItem : BaseEntity
{
    public Guid DisciplineId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public Discipline Discipline { get; set; } = null!;
}
