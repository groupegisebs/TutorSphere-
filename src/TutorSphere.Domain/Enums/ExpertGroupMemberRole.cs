namespace TutorSphere.Domain.Enums;

/// <summary>Rôle interne au sein d'un groupe d'experts (distinct du rôle Identity « Expert »).</summary>
public enum ExpertGroupMemberRole
{
    Expert = 0,
    Manager = 1,
    DisciplineLead = 2,
    CommitteeLead = 3
}
