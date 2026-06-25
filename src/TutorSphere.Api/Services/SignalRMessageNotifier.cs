using Microsoft.AspNetCore.SignalR;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Api.Hubs;

namespace TutorSphere.Api.Services;

public class SignalRMessageNotifier : IRealTimeMessaging
{
    private readonly IHubContext<MessagesHub> _hubContext;

    public SignalRMessageNotifier(IHubContext<MessagesHub> hubContext) => _hubContext = hubContext;

    public Task NotifyMessageReceivedAsync(string recipientUserId, MessageDto message, CancellationToken ct = default) =>
        _hubContext.Clients.User(recipientUserId).SendAsync("ReceiveMessage", message, ct);
}
