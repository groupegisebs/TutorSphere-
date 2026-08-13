namespace TutorSphere.Domain.Enums;

public enum ExpertMembershipInviteStatus
{
    Sent = 0,
    AcceptedByCandidate = 1,
    PendingMemberApproval = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5,
    Cancelled = 6,
    AwaitingAdminValidation = 7
}
