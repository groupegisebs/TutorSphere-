namespace TutorSphere.Application.DTOs.Parents;

/// <summary>
/// Messagerie parent. Aucun courriel, téléphone ni adresse — uniquement noms d'affichage et dossiers.
/// </summary>
public record ParentMailboxDto(
    IReadOnlyList<ParentMailboxChildDto> Children,
    IReadOnlyList<ParentMailboxThreadDto> Threads,
    bool AdminAvailable);

public record ParentMailboxChildDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? SchoolLevel,
    string? PhotoUrl);

public record ParentMailboxThreadDto(
    string ThreadId,
    string Channel,
    Guid? StudentId,
    string ChildFirstName,
    string PeerUserId,
    string PeerName,
    string? RoleLabel,
    string? SubjectLabel,
    string? GroupName,
    bool GroupVerified,
    string? TeacherProfileSlug,
    string? CaseNumber,
    string? Reason,
    string? LastPreview,
    DateTime? LastAt,
    int UnreadCount);

public record ParentMailboxDirectoryDto(
    Guid ChildId,
    string ChildFirstName,
    IReadOnlyList<ParentMailboxTeacherDto> Teachers,
    ParentMailboxGroupDto? Group,
    bool AdminAvailable,
    IReadOnlyList<ParentMailboxContextItemDto> Context,
    IReadOnlyList<ParentMailboxPickDto> Homework,
    IReadOnlyList<ParentMailboxPickDto> Documents);

public record ParentMailboxTeacherDto(
    string UserId,
    string DisplayName,
    string? Subject,
    string? TenantSlug,
    string? GroupName,
    bool GroupVerified);

public record ParentMailboxGroupDto(
    string? ManagerUserId,
    Guid GroupId,
    string Name,
    string? LogoUrl,
    bool Verified,
    string? LinkedTeacherUserId);

public record ParentMailboxContextItemDto(
    string Kind,
    string Label);

public record ParentMailboxPickDto(
    Guid Id,
    string Label);

public record ParentMailboxThreadDetailDto(
    ParentMailboxThreadDto Thread,
    IReadOnlyList<ParentMailboxMessageDto> Messages);

public record ParentMailboxMessageDto(
    Guid Id,
    bool FromParent,
    string Body,
    string? AttachmentType,
    string? AttachmentLabel,
    Guid? AttachmentId,
    DateTime CreatedAt,
    bool IsRead);

public record ParentMailboxComposeRequest(
    Guid ChildId,
    string Channel,
    string Reason,
    string Body,
    string? TeacherUserId = null,
    string? AttachmentType = null,
    Guid? AttachmentId = null,
    DateTime? AppointmentAt = null);

public record ParentMailboxReplyRequest(
    string Body,
    string? AttachmentType = null,
    Guid? AttachmentId = null,
    DateTime? AppointmentAt = null);
