namespace TutorSphere.Domain.Enums;

public enum ExpertDelegatedTaskType
{
    /// <summary>Compléter / créer le profil enseignant (présentation, docs, branding).</summary>
    CreateTeacherProfile = 0,
    /// <summary>Organiser l'agenda (disponibilités / créneaux) de l'enseignant.</summary>
    OrganizeTeacherAgenda = 1,
    /// <summary>Publier le profil public de l'enseignant.</summary>
    PublishTeacherProfile = 2
}

public enum ExpertDelegatedTaskStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}
