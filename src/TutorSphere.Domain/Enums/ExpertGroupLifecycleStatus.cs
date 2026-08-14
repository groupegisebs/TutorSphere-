namespace TutorSphere.Domain.Enums;

/// <summary>
/// Machine d'états du groupe d'experts :
/// Draft → Active ↔ Suspended → Archived.
/// Soft-désactivation (IsActive=false) = Suspended + fin du mandat + retrait Identity GroupManager.
/// Suspendre un mandat ne change pas le lifecycle du groupe.
/// </summary>
public enum ExpertGroupLifecycleStatus
{
    Draft = 0,
    Active = 1,
    Suspended = 2,
    Archived = 3
}
