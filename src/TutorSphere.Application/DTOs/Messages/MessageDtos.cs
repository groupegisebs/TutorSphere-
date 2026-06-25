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
