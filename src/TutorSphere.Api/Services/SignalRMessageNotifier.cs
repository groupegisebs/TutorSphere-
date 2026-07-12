using Microsoft.AspNetCore.SignalR;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Api.Hubs;

namespace TutorSphere.Api.Services;

public class SignalRMessageNotifier : IRealTimeMessaging
{
    private readonly IHubContext<MessagesHub> _hubContext;

    public SignalRMessageNotifier(IHubContext<MessagesHub> hubContext) => _hubContext = hubContext;

    public Task NotifyMessageReceivedAsync(string recipientUserId, MessageDto message, CancellationToken ct = default) =>
        _hubContext.Clients.User(recipientUserId).SendAsync("ReceiveMessage", message, ct);

    public async Task NotifyLessonStartedAsync(
        IEnumerable<string> recipientUserIds,
        LessonStartedNotificationDto notification,
        CancellationToken ct = default)
    {
        var ids = recipientUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return;

        await _hubContext.Clients.Users(ids).SendAsync("LessonStarted", notification, ct);
    }
}
