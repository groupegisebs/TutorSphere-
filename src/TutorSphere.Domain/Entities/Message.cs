using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Message : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    /// <summary>Vide si envoi purement externe (e-mail hors plateforme).</summary>
    public string RecipientUserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Adresse e-mail externe (envoi MailSender), éventuellement en plus du destinataire interne.</summary>
    public string? ExternalRecipientEmail { get; set; }
    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public string? EmailError { get; set; }

    public bool IsDraft { get; set; }
    public bool IsStarred { get; set; }
    public bool SenderDeleted { get; set; }
    public bool RecipientDeleted { get; set; }
    public bool SenderArchived { get; set; }
    public bool RecipientArchived { get; set; }
    public Guid? InReplyToMessageId { get; set; }
}
