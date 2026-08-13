using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Web.Services;

public sealed class MessageService
{
    private readonly ApiClient _api;

    public MessageService(ApiClient api) => _api = api;

    public async Task<List<ConversationDto>> GetConversationsAsync() =>
        await _api.GetAsync<List<ConversationDto>>("api/messages/conversations") ?? [];

    public async Task<List<MessageDto>> GetMessagesAsync(string otherUserId) =>
        await _api.GetAsync<List<MessageDto>>($"api/messages/conversations/{Uri.EscapeDataString(otherUserId)}") ?? [];

    public async Task<List<MessageRecipientDto>> SearchRecipientsAsync(string? query)
    {
        var q = string.IsNullOrWhiteSpace(query) ? "" : $"?q={Uri.EscapeDataString(query.Trim())}";
        return await _api.GetAsync<List<MessageRecipientDto>>($"api/messages/recipients{q}") ?? [];
    }

    public async Task<MessageDto?> SendMessageAsync(SendMessageRequest req) =>
        await _api.PostAsync<MessageDto>("api/messages", req);

    public async Task MarkConversationReadAsync(string otherUserId) =>
        await _api.PutAsync<object>($"api/messages/conversations/{Uri.EscapeDataString(otherUserId)}/read", new { });
}
