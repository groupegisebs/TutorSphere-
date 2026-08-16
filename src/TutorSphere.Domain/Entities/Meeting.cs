using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Meeting : BaseEntity
{
    public Guid? OrganizerGroupId { get; set; }
    public string OrganizerUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Agenda { get; set; }
    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Douala";
    public MeetingVisibility Visibility { get; set; } = MeetingVisibility.Private;
    public MeetingStatus Status { get; set; } = MeetingStatus.Draft;
    public bool IsImmediate { get; set; }
    public bool WaitingRoomEnabled { get; set; } = true;
    /// <summary>Code d'accès en clair : il doit être rappelé à l'organisateur et dans les invitations.</summary>
    public string? AccessCode { get; set; }
    public string? AccessCodeHash { get; set; }
    public bool AllowMic { get; set; } = true;
    public bool AllowCamera { get; set; } = true;
    public bool AllowScreenShare { get; set; } = true;
    public bool RecordingEnabled { get; set; }
    public bool TranscriptionEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool AiActivatedByOrganizer { get; set; }
    public string Language { get; set; } = "fr";
    public bool Remind24h { get; set; } = true;
    public bool Remind1h { get; set; } = true;
    public bool Remind10m { get; set; } = true;
    public bool SendEmailInvites { get; set; } = true;
    public bool Locked { get; set; }
    public DateTime? LiveStartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public MeetingMinutesShare MinutesShare { get; set; } = MeetingMinutesShare.ParticipantsOnly;
    public bool MinutesApproved { get; set; }
    public string? RetentionPolicy { get; set; }

    public ExpertGroup? OrganizerGroup { get; set; }
    public MeetingRecurrence? Recurrence { get; set; }
    public ICollection<MeetingGroup> Groups { get; set; } = [];
    public ICollection<MeetingParticipant> Participants { get; set; } = [];
    public ICollection<MeetingExternalGuest> ExternalGuests { get; set; } = [];
    public ICollection<MeetingInvitation> Invitations { get; set; } = [];
    public ICollection<MeetingSession> Sessions { get; set; } = [];
    public ICollection<MeetingFile> Files { get; set; } = [];
    public ICollection<MeetingRecording> Recordings { get; set; } = [];
    public ICollection<MeetingTranscript> Transcripts { get; set; } = [];
    public ICollection<MeetingAIConsent> AiConsents { get; set; } = [];
    public ICollection<MeetingAISummary> AiSummaries { get; set; } = [];
    public ICollection<MeetingDecision> Decisions { get; set; } = [];
    public ICollection<MeetingActionItem> ActionItems { get; set; } = [];
    public ICollection<MeetingNotification> Notifications { get; set; } = [];
    public ICollection<MeetingAuditLog> AuditLogs { get; set; } = [];
}

public class MeetingRecurrence : BaseEntity
{
    public Guid MeetingId { get; set; }
    public MeetingRecurrenceFrequency Frequency { get; set; } = MeetingRecurrenceFrequency.Weekly;
    public int Interval { get; set; } = 1;
    public string? ByDaysCsv { get; set; }
    public DateTime? UntilUtc { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingGroup : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Guid ExpertGroupId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public ExpertGroup ExpertGroup { get; set; } = null!;
}

public class MeetingParticipant : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string? UserId { get; set; }
    public Guid? ExternalGuestId { get; set; }
    public MeetingParticipantRole Role { get; set; } = MeetingParticipantRole.Participant;
    public MeetingParticipantStatus Status { get; set; } = MeetingParticipantStatus.Invited;
    public DateTime? JoinedAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public bool HandRaised { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public MeetingExternalGuest? ExternalGuest { get; set; }
}

public class MeetingExternalGuest : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Code d'invitation propre à cet invité : une seule saisie suffit pour entrer.</summary>
    public string? AccessCode { get; set; }
    public string? EmailVerifyCodeHash { get; set; }
    public DateTime TokenExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingInvitation : BaseEntity
{
    public Guid MeetingId { get; set; }
    public MeetingInvitationKind Kind { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientUserId { get; set; }
    public Guid? ExternalGuestId { get; set; }
    public MeetingInvitationStatus Status { get; set; } = MeetingInvitationStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingSession : BaseEntity
{
    public Guid MeetingId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public ICollection<MeetingAttendance> Attendances { get; set; } = [];
    public ICollection<MeetingMessage> Messages { get; set; } = [];
}

public class MeetingAttendance : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAtUtc { get; set; }
    public MeetingSession Session { get; set; } = null!;
    public MeetingParticipant Participant { get; set; } = null!;
}

public class MeetingMessage : BaseEntity
{
    public Guid SessionId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public MeetingSession Session { get; set; } = null!;
}

public class MeetingFile : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string StoragePath { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingRecording : BaseEntity
{
    public Guid MeetingId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? StoragePath { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingTranscript : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string Language { get; set; } = "fr";
    public string Content { get; set; } = string.Empty;
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingAIConsent : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string SubjectKey { get; set; } = string.Empty;
    public bool Consented { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingAISummary : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string? Overview { get; set; }
    public string? TopicsJson { get; set; }
    public string? OpenQuestionsJson { get; set; }
    public string? RisksJson { get; set; }
    public string? NextSteps { get; set; }
    public bool IsDraft { get; set; } = true;
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingDecision : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool FromAi { get; set; }
    public bool Accepted { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingActionItem : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public MeetingActionItemStatus Status { get; set; } = MeetingActionItemStatus.Proposed;
    public bool FromAi { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingNotification : BaseEntity
{
    public Guid MeetingId { get; set; }
    public MeetingNotificationKind Kind { get; set; }
    public string? RecipientUserId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTime? SentAtUtc { get; set; }
    public bool Failed { get; set; }
    public string? Error { get; set; }
    public Meeting Meeting { get; set; } = null!;
}

public class MeetingAuditLog : BaseEntity
{
    public Guid MeetingId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public Meeting Meeting { get; set; } = null!;
}
