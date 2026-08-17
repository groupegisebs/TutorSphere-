namespace TutorSphere.Infrastructure.WhatsApp;

/// <summary>
/// Codes fonctionnels envoyés à la passerelle. Chacun doit avoir une correspondance vers un modèle
/// approuvé par Meta (POST /api/whatsapp/templates), sinon l'envoi est refusé avant tout appel.
/// </summary>
public static class WhatsAppTemplates
{
    /// <summary>Code à six chiffres de vérification du numéro. Variable : Code.</summary>
    public const string VerificationCode = "WHATSAPP_VERIFY_CODE";

    /// <summary>
    /// Rappel de cours, même code fonctionnel que le courriel : les deux canaux restent alignés.
    /// Variables : RecipientName, TutorName, Subject, LessonDate.
    /// </summary>
    public const string LessonReminder = "LESSON_REMINDER";
}
