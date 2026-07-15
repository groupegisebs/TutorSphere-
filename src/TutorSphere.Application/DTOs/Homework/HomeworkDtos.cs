using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Homework;

public record HomeworkContentBlockDto(
    string Id,
    string Type,
    string? Title,
    string? Body,
    string? Url);

public record HomeworkCriterionDto(
    string Name,
    int Points);

public record CreateHomeworkRequest(
    Guid StudentId,
    Guid? LessonId,
    string Title,
    string? Description,
    DateTime? DueDate,
    string? Subject = null,
    string? Instructions = null,
    int? EstimatedMinutes = null,
    HomeworkSubmissionMode SubmissionModes = HomeworkSubmissionMode.Online,
    IReadOnlyList<HomeworkContentBlockDto>? Content = null,
    IReadOnlyList<HomeworkCriterionDto>? Criteria = null,
    bool IsDraft = false,
    Guid? AssignmentGroupId = null);

public record CreateHomeworkBatchRequest(
    IReadOnlyList<Guid> StudentIds,
    Guid? LessonId,
    string Title,
    string? Description,
    DateTime? DueDate,
    string? Subject = null,
    string? Instructions = null,
    int? EstimatedMinutes = null,
    HomeworkSubmissionMode SubmissionModes = HomeworkSubmissionMode.Online,
    IReadOnlyList<HomeworkContentBlockDto>? Content = null,
    IReadOnlyList<HomeworkCriterionDto>? Criteria = null,
    bool IsDraft = false);

public record UpdateHomeworkRequest(
    string Title,
    string? Description,
    DateTime? DueDate,
    string? Subject = null,
    string? Instructions = null,
    int? EstimatedMinutes = null,
    HomeworkSubmissionMode? SubmissionModes = null,
    IReadOnlyList<HomeworkContentBlockDto>? Content = null,
    IReadOnlyList<HomeworkCriterionDto>? Criteria = null,
    bool? IsDraft = null);

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
    Guid? AssignmentGroupId,
    string Title,
    string? Subject,
    string? Description,
    string? Instructions,
    DateTime? DueDate,
    int? EstimatedMinutes,
    HomeworkSubmissionMode SubmissionModes,
    IReadOnlyList<HomeworkContentBlockDto> Content,
    IReadOnlyList<HomeworkCriterionDto> Criteria,
    bool IsDraft,
    DateTime? SubmittedAt,
    string? SubmissionNotes,
    decimal? Grade,
    string? Feedback,
    bool IsGraded,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
