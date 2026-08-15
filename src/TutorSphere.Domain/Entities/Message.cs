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

    /// <summary>Canal parent : teacher | group | admin. Null = fil générique.</summary>
    public string? ParentChannel { get; set; }
    public Guid? StudentId { get; set; }
    public string? ParentReason { get; set; }
    /// <summary>Numéro de dossier assistance (ex. TS-2048).</summary>
    public string? CaseNumber { get; set; }
    /// <summary>homework | document | appointment</summary>
    public string? AttachmentType { get; set; }
    public Guid? AttachmentId { get; set; }
    public string? AttachmentLabel { get; set; }
}
