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
    string? SessionNotes);

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
