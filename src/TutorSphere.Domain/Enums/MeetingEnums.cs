namespace TutorSphere.Domain.Enums;

public enum MeetingVisibility
{
    Private = 0,
    CurrentGroup = 1,
    SelectedGroups = 2,
    International = 3,
    InvitationOnly = 4
}

public enum MeetingStatus
{
    Draft = 0,
    Scheduled = 1,
    Live = 2,
    Ended = 3,
    Cancelled = 4
}

public enum MeetingParticipantRole
{
    Organizer = 0,
    CoOrganizer = 1,
    Presenter = 2,
    Participant = 3,
    ExternalGuest = 4
}

public enum MeetingParticipantStatus
{
    Invited = 0,
    Accepted = 1,
    Declined = 2,
    Waiting = 3,
    InMeeting = 4,
    Left = 5,
    Denied = 6,
    Removed = 7,
    Absent = 8
}

public enum MeetingInvitationKind
{
    Internal = 0,
    External = 1
}

public enum MeetingInvitationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Revoked = 3,
    Consumed = 4
}

public enum MeetingActionItemStatus
{
    Proposed = 0,
    Accepted = 1,
    Rejected = 2,
    Edited = 3
}

public enum MeetingMinutesShare
{
    ParticipantsOnly = 0,
    Group = 1
}

public enum MeetingRecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum MeetingNotificationKind
{
    Invitation = 0,
    Updated = 1,
    Cancelled = 2,
    Reminder24h = 3,
    Reminder1h = 4,
    Reminder10m = 5,
    Minutes = 6,
    GuestCode = 7
}
