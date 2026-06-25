using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Application.Common.Interfaces;

public interface IRealTimeMessaging
{
    Task NotifyMessageReceivedAsync(string recipientUserId, MessageDto message, CancellationToken ct = default);
}
