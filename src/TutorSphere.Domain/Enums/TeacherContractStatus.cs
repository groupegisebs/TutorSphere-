namespace TutorSphere.Domain.Enums;

public enum TeacherContractStatus
{
    Draft = 0,
    Sent = 1,
    Viewed = 2,
    AwaitingSignature = 3,
    Signed = 4,
    Expired = 5,
    Refused = 6,
    Cancelled = 7,
    Replaced = 8
}

public enum TeacherContractAuditAction
{
    Created = 0,
    Sent = 1,
    Viewed = 2,
    SectionOpened = 3,
    SectionAccepted = 4,
    SectionRefused = 5,
    IdentityConfirmed = 6,
    Signed = 7,
    Expired = 8,
    Downloaded = 9,
    Cancelled = 10,
    Replaced = 11,
    Resent = 12
}
