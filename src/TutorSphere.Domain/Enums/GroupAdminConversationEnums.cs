namespace TutorSphere.Domain.Enums;

public enum GroupAdminConversationCategory
{
    Technical = 0,
    Governance = 1,
    ExpertAdmission = 2,
    Teacher = 3,
    Discipline = 4,
    Offer = 5,
    Pricing = 6,
    Payment = 7,
    Incident = 8,
    Compliance = 9,
    Administrative = 10,
    Other = 11
}

public enum GroupAdminConversationPriority
{
    Normal = 0,
    Important = 1,
    Urgent = 2,
    Critical = 3
}

public enum GroupAdminConversationStatus
{
    Open = 0,
    AdminResponseRequired = 1,
    GroupResponseRequired = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5,
    Reopened = 6
}

public enum TeacherInterestRequestStatus
{
    Submitted = 0,
    Routed = 1,
    InviteSent = 2,
    Declined = 3,
    Expired = 4
}
