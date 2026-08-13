using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertGroupGovernance;

public record ExpertGovernanceEventDto(
    Guid Id,
    Guid? ExpertGroupId,
    ExpertGovernanceEventType EventType,
    string ActorUserId,
    string? ActorName,
    string Summary,
    Guid? RelatedTenantId,
    Guid? RelatedEntityId,
    bool IsNotification,
    DateTime? ReadAtUtc,
    DateTime CreatedAt);

public record ExpertWorkspaceItemDto(
    Guid Id,
    Guid ExpertGroupId,
    ExpertWorkspaceItemType ItemType,
    ExpertWorkspaceItemStatus Status,
    string Title,
    string? Description,
    Guid? RelatedTeacherTenantId,
    string? RelatedTeacherName,
    string CreatedByUserId,
    string? AssignedToUserId,
    string? AssignedToName,
    DateTime? ScheduledAtUtc,
    DateTime? CompletedAtUtc,
    string? OutcomeNotes,
    DateTime CreatedAt);

public record CreateExpertWorkspaceItemRequest(
    ExpertWorkspaceItemType ItemType,
    string Title,
    string? Description = null,
    Guid? RelatedTeacherTenantId = null,
    string? AssignedToUserId = null,
    DateTime? ScheduledAtUtc = null);

public record CompleteExpertWorkspaceItemRequest(string? OutcomeNotes = null);
