using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>Affectation d'un enseignant (tenant) à une discipline par le groupe d'experts responsable.</summary>
public class TeacherDisciplineAssignment : BaseEntity
{
    public Guid DisciplineId { get; set; }
    public Guid TenantId { get; set; }
    public string AssignedByUserId { get; set; } = string.Empty;

    public Discipline Discipline { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
