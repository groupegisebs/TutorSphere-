namespace TutorSphere.Domain.Enums;

public enum WhatsAppEnrollmentStatus
{
    /// <summary>Numéro saisi, code envoyé, pas encore confirmé : aucun message métier ne part.</summary>
    PendingVerification = 0,

    Active = 1,

    /// <summary>Canal refusé ou révoqué. Conservé pour prouver le désabonnement.</summary>
    OptedOut = 2
}
