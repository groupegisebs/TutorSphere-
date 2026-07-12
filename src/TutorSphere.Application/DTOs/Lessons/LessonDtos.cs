using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Lessons;

public record CreateLessonRequest(
    string Title,
    string? Description,
    string? Subject,
    DateTime StartTime,
    DateTime EndTime,
    LessonMode Mode,
    string? Location,
    string? MeetingUrl,
    string? SessionNotes,
    IReadOnlyList<Guid>? StudentIds = null,
    /// <summary>none | weekly | biweekly | monthly</summary>
    string? RecurrenceFrequency = null,
    /// <summary>Total occurrences including the first (2–52). Ignored if RecurrenceUntil is set.</summary>
    int? RecurrenceOccurrences = null,
    /// <summary>Last date (inclusive) for recurring sessions. Takes precedence over Occurrences when both set.</summary>
    DateTime? RecurrenceUntil = null);

public record UpdateLessonRequest(
    string Title,
    string? Description,
    string? Subject,
    DateTime StartTime,
    DateTime EndTime,
    LessonMode Mode,
    string? Location,
    string? MeetingUrl,
    string? SessionNotes);

public record LessonDto(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    DateTime StartTime,
    DateTime EndTime,
    string Mode,
    string? Location,
    string? MeetingUrl,
    string? SessionNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
