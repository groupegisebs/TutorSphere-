using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Discipline / matière définie par un groupe d'experts pour un cycle scolaire donné.
/// Ce n'est pas une école : elle décrit un service d'accompagnement (services fournis + méthode
/// de travail) pour soutenir des élèves/étudiants ayant des besoins, dans le cadre de cette discipline.
/// Le groupe d'experts y affecte ensuite les enseignants de son groupe qu'il a sélectionnés.
/// </summary>
public class Discipline : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SchoolCycle Cycle { get; set; }

    /// <summary>Méthode de travail détaillée pour accompagner les élèves/étudiants dans cette discipline.</summary>
    public string? WorkMethod { get; set; }

    public bool IsActive { get; set; } = true;

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public ICollection<DisciplineServiceItem> Services { get; set; } = [];
    public ICollection<TeacherDisciplineAssignment> Assignments { get; set; } = [];
}
