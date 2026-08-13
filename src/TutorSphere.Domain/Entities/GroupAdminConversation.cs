using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class GroupAdminConversation : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public GroupAdminConversationCategory Category { get; set; }
    public GroupAdminConversationPriority Priority { get; set; } = GroupAdminConversationPriority.Normal;
    public GroupAdminConversationStatus Status { get; set; } = GroupAdminConversationStatus.Open;

    public string CreatedByManagerUserId { get; set; } = string.Empty;
    public string? AssignedAdminUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public ICollection<GroupAdminMessage> Messages { get; set; } = [];
}
