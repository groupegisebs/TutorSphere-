using TutorSphere.Application.DTOs.Settings;

namespace TutorSphere.Application.Common.Interfaces;

/// <summary>Résultat d'une opération d'inscription, avec le message à afficher tel quel.</summary>
public record WhatsAppChannelResult(bool Success, string? Error, WhatsAppChannelDto? Channel);

/// <summary>
/// Cycle de vie du canal WhatsApp d'un compte : saisie du numéro, vérification par code,
/// désabonnement. Aucune notification métier ne part avant la vérification.
/// </summary>
public interface IWhatsAppEnrollmentService
{
    Task<WhatsAppChannelDto> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Normalise le numéro, envoie un code à six chiffres et met le canal en attente.</summary>
    Task<WhatsAppChannelResult> StartAsync(string userId, string phone, CancellationToken ct = default);

    /// <summary>Active le canal si le code correspond, n'est pas expiré et n'a pas épuisé les tentatives.</summary>
    Task<WhatsAppChannelResult> ConfirmAsync(string userId, string code, CancellationToken ct = default);

    Task<WhatsAppChannelResult> SetPreferencesAsync(
        string userId, bool lessonReminders, CancellationToken ct = default);

    /// <summary>Révoque le canal. Le numéro reste en base pour attester du refus.</summary>
    Task<WhatsAppChannelResult> OptOutAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Notifications métier sur WhatsApp. Chaque envoi passe par un modèle approuvé par Meta et ne
/// contient jamais de données sensibles : le détail reste dans l'application, derrière la connexion.
/// </summary>
public interface IWhatsAppNotifier
{
    /// <summary>Vrai si le compte a un canal vérifié et accepte les rappels de cours.</summary>
    Task<bool> AcceptsLessonRemindersAsync(string userId, CancellationToken ct = default);

    Task SendLessonReminderAsync(
        string userId,
        string recipientName,
        string tutorName,
        string subject,
        DateTime lessonDate,
        CancellationToken ct = default);
}
