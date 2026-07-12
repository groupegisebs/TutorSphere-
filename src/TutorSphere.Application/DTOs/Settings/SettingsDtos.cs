namespace TutorSphere.Application.DTOs.Settings;

public record NotificationPreferencesDto(bool EmailLessonReminders);

public record UpdateNotificationPreferencesRequest(bool EmailLessonReminders);

public record CalendarFeedDto(
    bool IsEnabled,
    string? FeedUrl,
    string? WebcalUrl,
    string Instructions);

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

