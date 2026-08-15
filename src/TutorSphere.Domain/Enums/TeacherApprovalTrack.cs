namespace TutorSphere.Domain.Enums;

/// <summary>
/// Parcours de validation des enseignants défini par le groupe.
/// La démonstration pédagogique n'est pas obligatoire pour tous les groupes.
/// </summary>
public enum TeacherApprovalTrack
{
    /// <summary>Dossier → Approbation.</summary>
    FileOnly = 0,

    /// <summary>Dossier → Entretien → Approbation.</summary>
    FileThenInterview = 1,

    /// <summary>Dossier → Entretien → Démonstration → Approbation.</summary>
    FileInterviewThenDemonstration = 2,

    /// <summary>Dossier → Démonstration → Approbation.</summary>
    FileThenDemonstration = 3
}

public enum DemonstrationRecommendation
{
    None = 0,
    Approve = 1,
    Improve = 2,
    Redo = 3,
    Reject = 4
}

public static class TeacherApprovalTrackExtensions
{
    public static bool RequiresInterview(this TeacherApprovalTrack track) =>
        track is TeacherApprovalTrack.FileThenInterview
            or TeacherApprovalTrack.FileInterviewThenDemonstration;

    public static bool RequiresDemonstration(this TeacherApprovalTrack track) =>
        track is TeacherApprovalTrack.FileInterviewThenDemonstration
            or TeacherApprovalTrack.FileThenDemonstration;

    public static string Describe(this TeacherApprovalTrack track) => track switch
    {
        TeacherApprovalTrack.FileThenInterview => "Dossier → Entretien → Approbation",
        TeacherApprovalTrack.FileThenDemonstration => "Dossier → Démonstration → Approbation",
        TeacherApprovalTrack.FileInterviewThenDemonstration =>
            "Dossier → Entretien → Démonstration → Approbation",
        _ => "Dossier → Approbation"
    };
}
