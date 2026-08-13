namespace TutorSphere.Application.Options;

/// <summary>
/// Feature flags du centre Expert.
/// Enabled = module utilisable ; Frozen = visible au menu mais verrouillé (pas masqué).
/// </summary>
public class ExpertModuleFeatureOptions
{
    public const string SectionName = "ExpertModules";

    /// <summary>Modules réellement disponibles.</summary>
    public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Modules visibles mais freezés (badge « Bientôt disponible », non cliquables).
    /// Si absents de Enabled et Frozen, traités comme Frozen.
    /// </summary>
    public Dictionary<string, bool> Frozen { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string moduleKey) =>
        Enabled.TryGetValue(moduleKey, out var v) && v;

    public bool IsFrozen(string moduleKey)
    {
        if (IsEnabled(moduleKey)) return false;
        if (Frozen.TryGetValue(moduleKey, out var f)) return f;
        return true; // défaut : frozen, jamais présenté comme disponible
    }
}

public static class ExpertModuleKeys
{
    public const string Dashboard = "dashboard";
    public const string Approvals = "approvals";
    public const string Teachers = "teachers";
    public const string Disciplines = "disciplines";
    public const string Offers = "offers";
    public const string Members = "members";
    public const string Admissions = "admissions";
    public const string AdminChat = "adminChat";
    public const string Interviews = "interviews";
    public const string Demonstrations = "demonstrations";
    public const string Documents = "documents";
    public const string Renewals = "renewals";
    public const string Quality = "quality";
    public const string Observations = "observations";
    public const string Resources = "resources";
    public const string Feedback = "feedback";
    public const string Incidents = "incidents";
    public const string Library = "library";
    public const string Training = "training";
    public const string Visibility = "visibility";
    public const string Meetings = "meetings";
    public const string Decisions = "decisions";
    public const string Reports = "reports";
    public const string Activity = "activity";
    public const string Notifications = "notifications";
    public const string Profile = "profile";
    public const string GroupAdmin = "groupAdmin";
}
