namespace TutorSphere.Application.Common;

public enum TeacherPublicPhotoKind
{
    TeacherPhoto = 0,
    GroupLogo = 1,
    Initials = 2
}

public sealed record TeacherPublicPhoto(
    TeacherPublicPhotoKind Kind,
    string? Url,
    string Initials,
    bool IsGroupLogoFallback);

/// <summary>
/// Photo publique : photo enseignant, sinon logo du groupe, sinon initiales.
/// Ne remplace jamais une photo existante par le logo.
/// </summary>
public static class TeacherPublicPhotoResolver
{
    public static TeacherPublicPhoto Resolve(
        string? teacherProfilePhotoUrl,
        string? expertGroupLogoUrl,
        string? publicInitials)
    {
        var initials = string.IsNullOrWhiteSpace(publicInitials) ? "?" : publicInitials.Trim();

        if (!string.IsNullOrWhiteSpace(teacherProfilePhotoUrl))
        {
            return new TeacherPublicPhoto(
                TeacherPublicPhotoKind.TeacherPhoto,
                teacherProfilePhotoUrl.Trim(),
                initials,
                false);
        }

        if (!string.IsNullOrWhiteSpace(expertGroupLogoUrl))
        {
            return new TeacherPublicPhoto(
                TeacherPublicPhotoKind.GroupLogo,
                expertGroupLogoUrl.Trim(),
                initials,
                true);
        }

        return new TeacherPublicPhoto(TeacherPublicPhotoKind.Initials, null, initials, false);
    }

    public static string ToApi(TeacherPublicPhotoKind kind) => kind switch
    {
        TeacherPublicPhotoKind.TeacherPhoto => "teacherPhoto",
        TeacherPublicPhotoKind.GroupLogo => "groupLogo",
        _ => "initials"
    };
}
