using System.Text.Json;
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
    string? SubmissionNotes = null,
    HomeworkSubmissionMode Mode = HomeworkSubmissionMode.Online,
    IReadOnlyList<HomeworkSubmissionFileDto>? Attachments = null);

public record HomeworkSubmissionFileDto(
    Guid DocumentId,
    string FileName,
    string? Url = null);

/// <summary>Payload JSON stocké dans <see cref="HomeworkDto.SubmissionNotes"/> après remise.</summary>
public record HomeworkSubmissionPayload(
    HomeworkSubmissionMode Mode,
    string? Text,
    IReadOnlyList<HomeworkSubmissionFileDto> Attachments);

public record GradeHomeworkRequest(
    decimal Grade,
    string? Feedback);

public static class HomeworkJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static HomeworkSubmissionPayload? TryParseSubmission(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                return JsonSerializer.Deserialize<HomeworkSubmissionPayload>(trimmed, Options);
            }
            catch
            {
                /* plain-text legacy */
            }
        }

        return new HomeworkSubmissionPayload(HomeworkSubmissionMode.Online, trimmed, []);
    }
}

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
