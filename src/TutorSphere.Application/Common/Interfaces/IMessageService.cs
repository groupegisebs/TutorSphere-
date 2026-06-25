using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Application.Common.Interfaces;

public interface IMessageService
{
    Task<MessageDto> SendAsync(string senderUserId, SendMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(string userId, string otherUserId, CancellationToken ct = default);
    Task<MessageDto?> MarkAsReadAsync(string userId, Guid messageId, CancellationToken ct = default);
    Task MarkConversationAsReadAsync(string userId, string otherUserId, CancellationToken ct = default);
}
