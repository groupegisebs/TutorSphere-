namespace TutorSphere.Domain.Enums;

public enum GroupOfferStatus
{
    Draft = 0,
    UnderReview = 1,
    Approved = 2,
    Published = 3,
    Suspended = 4,
    Archived = 5
}

public enum GroupOfferPricingModel
{
    Fixed = 0,
    Range = 1,
    TeacherProposed = 2
}

public enum GroupOfferTeacherAssignmentStatus
{
    Invited = 0,
    Applied = 1,
    UnderReview = 2,
    Approved = 3,
    Active = 4,
    Suspended = 5,
    Removed = 6,
    Declined = 7
}
