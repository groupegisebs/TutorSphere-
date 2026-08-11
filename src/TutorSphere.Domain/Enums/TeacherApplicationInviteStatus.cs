namespace TutorSphere.Domain.Enums;

/// <summary>Statut d'une invitation enseignant envoyée par un expert.</summary>
public enum TeacherApplicationInviteStatus
{
    /// <summary>E-mail envoyé, pas encore d'inscription.</summary>
    Sent = 0,

    /// <summary>Le destinataire s'est inscrit (dossier en attente ou lié).</summary>
    Registered = 1,

    /// <summary>Dossier approuvé par un expert.</summary>
    Approved = 2,

    /// <summary>Dossier rejeté par un expert.</summary>
    Rejected = 3,

    /// <summary>Invitation expirée sans inscription.</summary>
    Expired = 4
}
