using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Application.Common.Interfaces;

public interface IMessageService
{
    Task<MessageDto> SendAsync(string senderUserId, SendMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(string userId, string otherUserId, CancellationToken ct = default);
    Task<MessageDto?> MarkAsReadAsync(string userId, Guid messageId, CancellationToken ct = default);
    Task MarkConversationAsReadAsync(string userId, string otherUserId, CancellationToken ct = default);

    /// <summary>
    /// Recherche de destinataires autorisés :
    /// élève → enseignants ; enseignant → élèves ;
    /// Responsable de groupe → membres + admins plateforme ;
    /// membre → autres membres du même groupe + Responsable.
    /// </summary>
    Task<IReadOnlyList<MessageRecipientDto>> SearchRecipientsAsync(
        string userId, string? query, CancellationToken ct = default);

    /// <summary>Admin plateforme : message interne à n'importe quel utilisateur (hors contrainte tenant).</summary>
    Task<MessageDto> SendAsPlatformAdminAsync(string adminUserId, SendMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetAdminConversationsAsync(string adminUserId, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDto>> GetAdminMessagesAsync(string adminUserId, string otherUserId, CancellationToken ct = default);
}
