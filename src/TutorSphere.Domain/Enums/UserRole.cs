namespace TutorSphere.Domain.Enums;

public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Tutor = "Tutor";
    public const string TeachingAssistant = "TeachingAssistant";
    public const string Parent = "Parent";
    public const string Student = "Student";

    public static readonly string[] All =
    [
        SuperAdmin,
        PlatformAdmin,
        Tutor,
        TeachingAssistant,
        Parent,
        Student
    ];

    /// <summary>Roles allowed to use the parent portal API and UI.</summary>
    public const string ParentPortalAccess = $"{Parent},{SuperAdmin},{PlatformAdmin}";

    public static readonly string[] ParentPortalRoles = [Parent, SuperAdmin, PlatformAdmin];
}
