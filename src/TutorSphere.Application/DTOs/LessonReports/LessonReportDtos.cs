namespace TutorSphere.Application.DTOs.LessonReports;

public record CreateLessonReportRequest(
    Guid LessonId,
    Guid StudentId,
    string? TopicsStudied,
    string? Participation,
    string? Strengths,
    string? Weaknesses,
    string? HomeworkAssigned,
    string? Observations);

public record UpdateLessonReportRequest(
    string? TopicsStudied,
    string? Participation,
    string? Strengths,
    string? Weaknesses,
    string? HomeworkAssigned,
    string? Observations);

public record LessonReportDto(
    Guid Id,
    Guid TenantId,
    Guid LessonId,
    Guid StudentId,
    string? TopicsStudied,
    string? Participation,
    string? Strengths,
    string? Weaknesses,
    string? HomeworkAssigned,
    string? Observations,
    bool SentToParent,
    DateTime? SentAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
