namespace TutorSphere.Application.DTOs.Lessons;

/// <summary>Pushed over SignalR when a tutor starts a live classroom session.</summary>
public record LessonStartedNotificationDto(
    Guid LessonId,
    string Title,
    string? Subject,
    string TutorName,
    DateTime StartedAtUtc);
