namespace TutorSphere.Domain.Enums;

/// <summary>Validation d'un enseignant (tenant) par un groupe d'experts éducatifs.</summary>
public enum ExpertApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    ChangesRequested = 3,
    UnderReview = 4,
    Assigned = 5,
    Suspended = 6
}
