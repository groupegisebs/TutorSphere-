using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Message : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
