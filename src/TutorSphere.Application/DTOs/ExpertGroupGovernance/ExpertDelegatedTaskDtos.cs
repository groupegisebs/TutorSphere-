using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertGroupGovernance;

public record ExpertDelegatedTaskDto(
    Guid Id,
    Guid ExpertGroupId,
    Guid TeacherTenantId,
    string TeacherSchoolName,
    ExpertDelegatedTaskType TaskType,
    ExpertDelegatedTaskStatus Status,
    string CreatedByManagerUserId,
    string AssigneeExpertUserId,
    string? AssigneeName,
    string? Notes,
    DateTime? DueAtUtc,
    DateTime CreatedAt,
    DateTime? CompletedAtUtc,
    string? CompletionNotes,
    bool TeacherIsPublicProfile);

public record CreateExpertDelegatedTaskRequest(
    Guid TeacherTenantId,
    string AssigneeExpertUserId,
    ExpertDelegatedTaskType TaskType,
    string? Notes = null,
    DateTime? DueAtUtc = null);

public record CompleteExpertDelegatedTaskRequest(string? CompletionNotes = null);
