using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class TeacherContract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;
    public string Version { get; set; } = "2026.1";
    public string Language { get; set; } = "fr";
    public TeacherContractStatus Status { get; set; } = TeacherContractStatus.Draft;

    public Guid TenantId { get; set; }
    public Guid ExpertGroupId { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;

    public string PlaceholdersJson { get; set; } = "{}";

    public string SignToken { get; set; } = string.Empty;
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? TokenInvalidatedAt { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime? RefusedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ExpiredAt { get; set; }

    public string? RefusedSectionKey { get; set; }
    public string? RefusalComment { get; set; }

    public string? TeacherTypedName { get; set; }
    public string? SignaturePngBase64 { get; set; }
    public string? TeacherIp { get; set; }
    public string? TeacherUserAgent { get; set; }

    public string? PdfUrl { get; set; }
    public string? DocumentHash { get; set; }
    public string? VerificationCode { get; set; }

    public Guid? ReplacedByContractId { get; set; }
    public Guid? ReplacesContractId { get; set; }

    public Tenant? Tenant { get; set; }
    public ExpertGroup? ExpertGroup { get; set; }
    public ICollection<TeacherContractSectionDecision> SectionDecisions { get; set; } = [];
    public ICollection<TeacherContractAuditEvent> AuditEvents { get; set; } = [];
}

public class TeacherContractSectionDecision : BaseEntity
{
    public Guid ContractId { get; set; }
    public string SectionKey { get; set; } = string.Empty;
    public bool? Accepted { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }

    public TeacherContract Contract { get; set; } = null!;
}

public class TeacherContractAuditEvent : BaseEntity
{
    public Guid ContractId { get; set; }
    public TeacherContractAuditAction Action { get; set; }
    public string? ActorUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }

    public TeacherContract Contract { get; set; } = null!;
}
