namespace TutorSphere.Application.DTOs.Settings;

public record NotificationPreferencesDto(bool EmailLessonReminders);

public record UpdateNotificationPreferencesRequest(bool EmailLessonReminders);

public record CalendarFeedDto(
    bool IsEnabled,
    string? FeedUrl,
    string? WebcalUrl,
    string Instructions);

/// <summary>
/// État du canal WhatsApp d'un compte. Le numéro n'est renvoyé que masqué : l'écran de réglages
/// n'a pas besoin du numéro complet pour confirmer à la personne lequel est enregistré.
/// </summary>
public record WhatsAppChannelDto(
    string Status,
    string? PhoneMasked,
    bool LessonReminders,
    DateTime? VerifiedAt,
    DateTime? ConsentAt,
    DateTime? CodeExpiresAt,
    int RemainingAttempts);

public record StartWhatsAppEnrollmentRequest(string Phone);

public record ConfirmWhatsAppEnrollmentRequest(string Code);

public record UpdateWhatsAppPreferencesRequest(bool LessonReminders);

public record UserProfileDto(
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? Phone,
    string? Bio,
    string PreferredLanguage,
    string TimeZone,
    string Role);

public record UpdateUserProfileRequest(
    string FirstName,
    string LastName,
    string? Phone = null,
    string? Bio = null,
    string? PreferredLanguage = null,
    string? TimeZone = null);

