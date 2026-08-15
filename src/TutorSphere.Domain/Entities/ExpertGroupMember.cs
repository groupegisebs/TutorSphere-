using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Lien utilisateur Identity ↔ groupe d'experts.</summary>
public class ExpertGroupMember : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public ExpertMembershipStatus Status { get; set; } = ExpertMembershipStatus.Active;
    public ExpertAdmissionMethod AdmissionMethod { get; set; } = ExpertAdmissionMethod.AdminDirect;
    public ExpertGroupMemberRole MemberRole { get; set; } = ExpertGroupMemberRole.Expert;

    public string? Specialty { get; set; }
    public DateTime? AdmittedAtUtc { get; set; }
    public string? ApprovedByAdminId { get; set; }
    public int? ApprovalCount { get; set; }
    public int? RequiredApprovalCount { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public string? InvitedByUserId { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public string? SuspensionReason { get; set; }

    /// <summary>Clés de permissions JSON (ex. ["teachers.view","admissions.vote"]).</summary>
    public string? PermissionsJson { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
}
