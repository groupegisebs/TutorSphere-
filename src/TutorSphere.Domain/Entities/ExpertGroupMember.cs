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
    public string? Specialty { get; set; }
    public DateTime? AdmittedAtUtc { get; set; }
    public string? ApprovedByAdminId { get; set; }
    public int? ApprovalCount { get; set; }
    public int? RequiredApprovalCount { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
}
