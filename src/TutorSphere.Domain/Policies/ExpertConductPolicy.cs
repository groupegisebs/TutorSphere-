namespace TutorSphere.Domain.Policies;

/// <summary>
/// Code de conduite des Experts TutorSphere — indépendance des revues, confidentialité des
/// dossiers enseignants, vote collégial et qualité pédagogique des offres de groupe.
/// L'acceptation est obligatoire pour soumettre une candidature d'admission.
/// </summary>
public static class ExpertConductPolicy
{
    public const string CurrentVersion = "2026.08";

    public const string PublicPath = "/legal/expert-conduct";

    public const string PrivacyPath = "/legal/privacy";

    public static bool IsCurrent(string? acceptedVersion) =>
        string.Equals(acceptedVersion?.Trim(), CurrentVersion, StringComparison.OrdinalIgnoreCase);
}
