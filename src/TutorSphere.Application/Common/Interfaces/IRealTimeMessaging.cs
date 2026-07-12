using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Application.Common.Interfaces;

public interface IRealTimeMessaging
{
    Task NotifyMessageReceivedAsync(string recipientUserId, MessageDto message, CancellationToken ct = default);

    Task NotifyLessonStartedAsync(
        IEnumerable<string> recipientUserIds,
        LessonStartedNotificationDto notification,
        CancellationToken ct = default);
}
