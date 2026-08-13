using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class GroupAdminMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? AttachmentReference { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }
    public string? PreviousBody { get; set; }

    public GroupAdminConversation Conversation { get; set; } = null!;
}
