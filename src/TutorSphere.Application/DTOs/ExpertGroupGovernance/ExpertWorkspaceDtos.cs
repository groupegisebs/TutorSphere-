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

/// <summary>Une page du journal d'activité, avec le total pour caler la pagination.</summary>
public record ExpertGovernanceEventPageDto(
    IReadOnlyList<ExpertGovernanceEventDto> Items,
    int Total,
    int Page,
    int PageSize);

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
    DateTime CreatedAt,
    string? PayloadJson = null);

public record CreateExpertWorkspaceItemRequest(
    ExpertWorkspaceItemType ItemType,
    string Title,
    string? Description = null,
    Guid? RelatedTeacherTenantId = null,
    string? AssignedToUserId = null,
    DateTime? ScheduledAtUtc = null,
    string? PayloadJson = null);

public record CompleteExpertWorkspaceItemRequest(string? OutcomeNotes = null);
