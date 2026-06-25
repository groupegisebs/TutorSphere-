namespace TutorSphere.Application.DTOs.Homework;

public record CreateHomeworkRequest(
    Guid StudentId,
    Guid? LessonId,
    string Title,
    string? Description,
    DateTime? DueDate);

public record UpdateHomeworkRequest(
    string Title,
    string? Description,
    DateTime? DueDate);

public record SubmitHomeworkRequest(
    string? SubmissionNotes);

public record GradeHomeworkRequest(
    decimal Grade,
    string? Feedback);

public record HomeworkDto(
    Guid Id,
    Guid TenantId,
    Guid StudentId,
    Guid? LessonId,
    string Title,
    string? Description,
    DateTime? DueDate,
    DateTime? SubmittedAt,
    string? SubmissionNotes,
    decimal? Grade,
    string? Feedback,
    bool IsGraded,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
