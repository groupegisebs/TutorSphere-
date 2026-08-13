using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Invitation d'un candidat Expert par un membre du groupe (admission par vote).</summary>
public class ExpertMembershipInvite : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public string InvitedByUserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Specialty { get; set; }
    public string? IntendedRole { get; set; }
    public string? Presentation { get; set; }
    public string? Justification { get; set; }
    public string? PersonalMessage { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime InviteExpiresAtUtc { get; set; }
    public DateTime? VoteOpenedAtUtc { get; set; }
    public DateTime? VoteExpiresAtUtc { get; set; }

    public ExpertMembershipInviteStatus Status { get; set; } = ExpertMembershipInviteStatus.Sent;
    public string? CandidateUserId { get; set; }

    /// <summary>CSV des UserId éligibles figés à l'ouverture du vote.</summary>
    public string EligibleVoterUserIdsCsv { get; set; } = string.Empty;
    public int RequiredApprovalCount { get; set; }
    public bool ConductAccepted { get; set; }
    public bool PrivacyAccepted { get; set; }
    public DateTime? CandidateSubmittedAtUtc { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? AdminClosedByUserId { get; set; }
    public string? AdminNotes { get; set; }

    public ExpertGroup? ExpertGroup { get; set; }
    public ICollection<ExpertMembershipVote> Votes { get; set; } = [];
}
