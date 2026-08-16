using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Meetings;

public record MeetingListItemDto(
    Guid Id,
    string Title,
    MeetingStatus Status,
    MeetingVisibility Visibility,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string TimeZoneId,
    string OrganizerName,
    int ParticipantCount,
    bool AiEnabled,
    bool IsOrganizer);

public record MeetingDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? Agenda,
    MeetingStatus Status,
    MeetingVisibility Visibility,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string TimeZoneId,
    string OrganizerUserId,
    string OrganizerName,
    Guid? OrganizerGroupId,
    bool WaitingRoomEnabled,
    bool AllowMic,
    bool AllowCamera,
    bool AllowScreenShare,
    bool RecordingEnabled,
    bool TranscriptionEnabled,
    bool AiEnabled,
    bool AiActivatedByOrganizer,
    bool Locked,
    string Language,
    bool Recurring,
    IReadOnlyList<Guid> GroupIds,
    IReadOnlyList<MeetingParticipantDto> Participants,
    IReadOnlyList<MeetingExternalGuestDto> ExternalGuests,
    IReadOnlyList<string> Permissions,
    bool RequiresAccessCode = false,
    string? AccessCode = null);

public record MeetingParticipantDto(
    Guid Id,
    string? UserId,
    string DisplayName,
    string? Email,
    string? RoleLabel,
    string? GroupName,
    string? Country,
    string? PhotoUrl,
    MeetingParticipantRole Role,
    MeetingParticipantStatus Status,
    int DurationSeconds,
    bool IsExternal);

public record MeetingExternalGuestDto(
    Guid Id,
    string FullName,
    string Email,
    DateTime TokenExpiresAtUtc,
    bool Revoked,
    bool Verified,
    string? AccessCode = null);

public record CreateMeetingRequest(
    string Title,
    string? Description,
    string? Agenda,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string? TimeZoneId,
    MeetingVisibility Visibility,
    IReadOnlyList<Guid>? GroupIds,
    IReadOnlyList<string>? InternalUserIds,
    IReadOnlyList<ExternalGuestInput>? ExternalGuests,
    bool Recurring,
    MeetingRecurrenceFrequency RecurrenceFrequency = MeetingRecurrenceFrequency.Weekly,
    bool WaitingRoomEnabled = true,
    string? AccessCode = null,
    bool AllowMic = true,
    bool AllowCamera = true,
    bool AllowScreenShare = true,
    bool RecordingEnabled = false,
    bool TranscriptionEnabled = false,
    bool AiEnabled = false,
    string Language = "fr",
    bool Remind24h = true,
    bool Remind1h = true,
    bool Remind10m = true,
    bool SendEmailInvites = true,
    string SaveMode = "draft");

public record ExternalGuestInput(string FullName, string Email);

public record MeetingCandidateDto(
    string Key,
    string Kind,
    string? UserId,
    Guid? GroupId,
    string DisplayName,
    string? Email,
    string? RoleLabel,
    string? GroupName,
    string? Country,
    string? PhotoUrl);

public record MeetingCandidatePageDto(
    IReadOnlyList<MeetingCandidateDto> Items,
    int Total,
    int Page,
    int PageSize);

public record GuestPreviewDto(
    Guid MeetingId,
    string Title,
    DateTime? StartAtUtc,
    string OrganizerName,
    bool RequiresAccessCode,
    bool WaitingRoomEnabled,
    bool RecordingEnabled,
    bool AiEnabled);

public record GuestEnterRequest(string Token, string DisplayName, string? AccessCode, string? EmailCode);

public record SetMeetingAccessCodeRequest(string? Code);

public record MeetingAccessCodeDto(string Code);

public record VerifyMeetingAccessCodeRequest(string? Code);

public record GuestEnterResult(Guid MeetingId, string DisplayName, bool Waiting);

public record MeetingMinutesDto(
    MeetingDetailDto Meeting,
    MeetingAISummaryDto? Summary,
    IReadOnlyList<MeetingDecisionDto> Decisions,
    IReadOnlyList<MeetingActionItemDto> Actions,
    IReadOnlyList<MeetingParticipantDto> Attendance,
    IReadOnlyList<string> Chat,
    IReadOnlyList<string> Files,
    bool RecordingAvailable,
    string? Transcript);

public record MeetingAISummaryDto(
    Guid Id,
    string? Overview,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Risks,
    string? NextSteps,
    bool IsDraft);

public record MeetingDecisionDto(Guid Id, string Text, bool FromAi, bool Accepted);
public record MeetingActionItemDto(
    Guid Id,
    string Title,
    string? AssigneeUserId,
    string? AssigneeName,
    DateTime? DueAtUtc,
    MeetingActionItemStatus Status,
    bool FromAi);

public record ReviewActionItemRequest(MeetingActionItemStatus Status, string? Title, string? AssigneeUserId, string? AssigneeName, DateTime? DueAtUtc);
public record SetAiConsentRequest(bool Consented);
public record ReviewDecisionRequest(bool Accepted);
public record MeetingGroupOptionDto(Guid Id, string Name, string? Country);
public record AdmitParticipantRequest(bool Admit);
public record SetParticipantRoleRequest(MeetingParticipantRole Role);
public record LockMeetingRequest(bool Locked);
public record ToggleRecordingRequest(bool Recording);
public record ShareMinutesRequest(MeetingMinutesShare Share);
public record SendMinutesEmailRequest(IReadOnlyList<string>? ExtraEmails);
public record PersistChatRequest(string SenderName, string Body);
