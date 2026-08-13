namespace TutorSphere.Domain.Enums;

public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Tutor = "Tutor";
    public const string TeachingAssistant = "TeachingAssistant";
    public const string Parent = "Parent";
    public const string Student = "Student";
    /// <summary>Expert éducatif — valide les fiches enseignants via un groupe d'experts.</summary>
    public const string Expert = "Expert";
    /// <summary>Responsable de groupe — Expert avec privilèges d'organisation (un par groupe).</summary>
    public const string GroupManager = "GroupManager";

    public static readonly string[] All =
    [
        SuperAdmin,
        PlatformAdmin,
        Tutor,
        TeachingAssistant,
        Parent,
        Student,
        Expert,
        GroupManager
    ];

    /// <summary>Roles allowed to use the parent portal API and UI (pas les admins plateforme).</summary>
    public const string ParentPortalAccess = Parent;

    public static readonly string[] ParentPortalRoles = [Parent];
}
