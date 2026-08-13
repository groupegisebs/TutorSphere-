using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class ExpertMembershipVote : BaseEntity
{
    public Guid InviteId { get; set; }
    public string VoterUserId { get; set; } = string.Empty;
    public ExpertMembershipVoteChoice Choice { get; set; }
    public string? Comment { get; set; }
    public DateTime VotedAtUtc { get; set; } = DateTime.UtcNow;

    public ExpertMembershipInvite Invite { get; set; } = null!;
}
