namespace TutorSphere.Application.DTOs.Settings;

public record NotificationPreferencesDto(bool EmailLessonReminders);

public record UpdateNotificationPreferencesRequest(bool EmailLessonReminders);

public record CalendarFeedDto(
    bool IsEnabled,
    string? FeedUrl,
    string? WebcalUrl,
    string Instructions);
