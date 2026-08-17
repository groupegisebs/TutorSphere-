using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Inscription d'une personne au canal WhatsApp. Table dédiée plutôt que des colonnes ajoutées à
/// ParentProfile, Student et ApplicationUser : le numéro utilisé pour notifier est ainsi unique,
/// vérifié, et l'historique du consentement reste opposable.
/// </summary>
public class WhatsAppEnrollment : BaseEntity
{
    /// <summary>Compte propriétaire du canal. Le parent reçoit les notifications de ses enfants.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Numéro au format E.164 sans le signe plus, tel qu'attendu par l'API Cloud.</summary>
    public string PhoneE164 { get; set; } = string.Empty;

    public string Language { get; set; } = "fr";

    public WhatsAppEnrollmentStatus Status { get; set; } = WhatsAppEnrollmentStatus.PendingVerification;

    /// <summary>
    /// Empreinte du code à six chiffres. Le code en clair n'est jamais stocké : une base lue
    /// donnerait sinon le moyen d'activer le canal sur un numéro tiers.
    /// </summary>
    public string? VerificationCodeHash { get; set; }

    public DateTime? VerificationSentAt { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }

    /// <summary>Tentatives de saisie erronées depuis le dernier envoi de code.</summary>
    public int VerificationAttempts { get; set; }

    public DateTime? VerifiedAt { get; set; }
    public DateTime? ConsentAt { get; set; }

    /// <summary>Origine du consentement (ex. « parent-settings »), pour tracer qui a accepté et où.</summary>
    public string? ConsentSource { get; set; }

    public DateTime? OptOutAt { get; set; }

    /// <summary>Rappels de cours sur ce canal. Indépendant du réglage courriel.</summary>
    public bool LessonReminders { get; set; } = true;

    /// <summary>Vrai lorsque le canal peut réellement servir à notifier.</summary>
    public bool IsUsable => Status == WhatsAppEnrollmentStatus.Active && VerifiedAt is not null;
}
