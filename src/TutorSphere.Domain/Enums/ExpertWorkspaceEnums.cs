namespace TutorSphere.Domain.Enums;

public enum ExpertWorkspaceItemType
{
    Interview = 0,
    Demonstration = 1,
    Renewal = 2,
    Observation = 3,
    Library = 4,
    Training = 5,
    Meeting = 6,
    Decision = 7,
    Incident = 8
}

public enum ExpertWorkspaceItemStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Cancelled = 3
}

public enum ExpertGovernanceEventType
{
    CaseAssigned = 0,
    CaseReviewStarted = 1,
    TeacherApproved = 2,
    TeacherRejected = 3,
    TeacherChangesRequested = 4,
    MembershipInviteCreated = 5,
    MembershipVoted = 6,
    MembershipDecided = 7,
    ManagerAppointed = 8,
    ManagerSuspended = 9,
    WorkspaceItemCreated = 10,
    WorkspaceItemCompleted = 11,
    DelegatedTaskCreated = 12,
    GroupSettingsUpdated = 13,
    RemarkAdded = 14,
    GroupOfferCreated = 15,
    GroupOfferPublished = 16,
    GroupAdminChatOpened = 17,
    GroupAdminChatMessagePosted = 18,
    ManagerMandateEnded = 19,
    MemberRoleChanged = 20,
    MemberSuspended = 21,
    MemberReactivated = 22,
    MemberRemovedFromGroup = 23,
    MemberPermissionsUpdated = 24,
    GroupDefinedRoleCreated = 25,
    MeetingCreated = 26,
    MeetingUpdated = 27,
    MeetingCancelled = 28,
    MeetingStarted = 29,
    MeetingEnded = 30,
    TeacherRegisteredViaInviteLink = 31,
    TeacherContractSent = 32,
    TeacherContractSigned = 33,
    TeacherContractRefused = 34
}
