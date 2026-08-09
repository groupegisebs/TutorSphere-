namespace TutorSphere.Domain.Policies;

/// <summary>
/// Code de conduite enseignant TutorSphere — aligné sur les standards professionnels
/// canadiens (ex. OCT Ontario, Professional Standards BC, Alberta Teaching Quality Standard /
/// Professional Conduct, Loi sur l'instruction publique du Québec / Loi 47, devoir de
/// signalement et protection des personnes mineures ou vulnérables).
/// L'acceptation est obligatoire à l'inscription et protège la plateforme et les usagers.
/// </summary>
public static class TeacherConductPolicy
{
    /// <summary>Incrémentez à chaque révision substantielle du texte.</summary>
    public const string CurrentVersion = "2026.08";

    public const string PublicPath = "/legal/teacher-conduct";

    public static bool IsCurrent(string? acceptedVersion) =>
        string.Equals(acceptedVersion?.Trim(), CurrentVersion, StringComparison.OrdinalIgnoreCase);
}
