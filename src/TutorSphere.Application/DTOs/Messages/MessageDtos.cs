using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.Messages;

public record SendMessageRequest(string RecipientUserId, string Subject, string Body);

public record MessageDto(
    Guid Id,
    string SenderUserId,
    string RecipientUserId,
    string Subject,
    string Body,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

public record ConversationDto(
    string OtherUserId,
    string OtherUserName,
    MessageDto? LastMessage,
    int UnreadCount);

public record MessageRecipientDto(
    string UserId,
    string DisplayName,
    string? Email,
    string Role);

public record AdminComposeMessageRequest(
    string? RecipientUserId,
    string? ExternalEmail,
    string Subject,
    string Body,
    bool SendEmailCopy = true,
    bool SaveAsDraft = false,
    Guid? InReplyToMessageId = null);

public record MailboxFolderCountsDto(
    int Inbox,
    int Sent,
    int Drafts,
    int Archive,
    int Trash,
    int Starred,
    int UnreadInbox);

public record MailboxMessageListItemDto(
    Guid Id,
    string Subject,
    string Preview,
    string CounterpartName,
    string? CounterpartEmail,
    string? CounterpartRole,
    string CounterpartUserId,
    bool IsOutbound,
    bool IsRead,
    bool IsStarred,
    bool EmailSent,
    DateTime CreatedAt,
    bool HasExternalRecipient);

public record MailboxMessageDetailDto(
    Guid Id,
    string Subject,
    string Body,
    string SenderUserId,
    string SenderName,
    string? SenderEmail,
    string RecipientUserId,
    string RecipientName,
    string? RecipientEmail,
    string? RecipientRole,
    string? ExternalRecipientEmail,
    bool IsOutbound,
    bool IsRead,
    bool IsStarred,
    bool IsDraft,
    bool EmailSent,
    string? EmailError,
    DateTime? EmailSentAt,
    DateTime CreatedAt,
    Guid? InReplyToMessageId,
    MailboxFolder Folder);

public record AdminMailboxSendResultDto(
    MailboxMessageDetailDto Message,
    bool EmailSent,
    string? EmailError);
