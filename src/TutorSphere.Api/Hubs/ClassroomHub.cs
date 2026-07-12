using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TutorSphere.Application.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Hubs;

/// <summary>
/// Realtime collaborative whiteboard for a lesson (strokes, clear, shared document background).
/// </summary>
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Student},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class ClassroomHub : Hub
{
    private static readonly ConcurrentDictionary<string, LessonBoardState> States = new(StringComparer.Ordinal);

    public async Task JoinLesson(Guid lessonId)
    {
        var group = GroupName(lessonId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);

        if (States.TryGetValue(group, out var state) && !string.IsNullOrEmpty(state.BackgroundDocumentId))
        {
            await Clients.Caller.SendAsync(
                "BoardBackgroundChanged",
                new BoardBackgroundDto(lessonId, state.BackgroundDocumentId, state.BackgroundContentType));
        }
    }

    public Task LeaveLesson(Guid lessonId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(lessonId));

    public Task SendStroke(Guid lessonId, BoardStrokeDto stroke) =>
        Clients.OthersInGroup(GroupName(lessonId)).SendAsync("BoardStroke", stroke);

    public async Task ClearBoard(Guid lessonId)
    {
        var group = GroupName(lessonId);
        var state = States.GetOrAdd(group, _ => new LessonBoardState());
        state.BackgroundDocumentId = null;
        state.BackgroundContentType = null;
        await Clients.OthersInGroup(group).SendAsync("BoardCleared", lessonId);
    }

    public async Task SetBackground(Guid lessonId, string? documentId, string? contentType)
    {
        var group = GroupName(lessonId);
        var state = States.GetOrAdd(group, _ => new LessonBoardState());
        state.BackgroundDocumentId = string.IsNullOrWhiteSpace(documentId) ? null : documentId.Trim();
        state.BackgroundContentType = contentType;
        await Clients.OthersInGroup(group).SendAsync(
            "BoardBackgroundChanged",
            new BoardBackgroundDto(lessonId, state.BackgroundDocumentId, state.BackgroundContentType));
    }

    private static string GroupName(Guid lessonId) => $"lesson:{lessonId:D}";

    private sealed class LessonBoardState
    {
        public string? BackgroundDocumentId { get; set; }
        public string? BackgroundContentType { get; set; }
    }
}

public record BoardStrokeDto(
    string Phase,      // start | move | end
    double X,          // 0–1 normalized
    double Y,
    string Tool,       // pen | eraser
    string Color,
    double Width,
    string? SenderId = null);

public record BoardBackgroundDto(
    Guid LessonId,
    string? DocumentId,
    string? ContentType);
