using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class ExpertWorkspaceItem : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public ExpertWorkspaceItemType ItemType { get; set; }
    public ExpertWorkspaceItemStatus Status { get; set; } = ExpertWorkspaceItemStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? RelatedTeacherTenantId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? OutcomeNotes { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public Tenant? RelatedTeacherTenant { get; set; }
}

public class ExpertGovernanceEvent : BaseEntity
{
    public Guid? ExpertGroupId { get; set; }
    public ExpertGovernanceEventType EventType { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid? RelatedTenantId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? PayloadJson { get; set; }
    public bool IsNotification { get; set; } = true;
    public DateTime? ReadAtUtc { get; set; }

    public ExpertGroup? ExpertGroup { get; set; }
}
