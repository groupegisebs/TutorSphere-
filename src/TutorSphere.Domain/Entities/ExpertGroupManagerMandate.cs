using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Historique des mandats de Responsable de groupe (un seul Active par groupe).</summary>
public class ExpertGroupManagerMandate : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public Guid MembershipId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public ExpertGroupManagerMandateStatus Status { get; set; } = ExpertGroupManagerMandateStatus.PendingActivation;

    public string? FunctionTitle { get; set; }
    public string? Phone { get; set; }

    public DateTime MandateStartsAtUtc { get; set; }
    public DateTime? MandateEndsAtUtc { get; set; }

    public string AppointedByAdminId { get; set; } = string.Empty;
    public string? EndedByAdminId { get; set; }
    public string? EndReason { get; set; }

    /// <summary>True si mandat temporaire / délégation.</summary>
    public bool IsTemporary { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public ExpertGroupMember Membership { get; set; } = null!;
}
