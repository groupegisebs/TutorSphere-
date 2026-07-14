using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TutorSphere.Application.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Hubs;

/// <summary>
/// Realtime classroom: collaborative whiteboard + presence + WebRTC signaling (A/V 1:1 / petit groupe).
/// </summary>
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Student},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class ClassroomHub : Hub
{
    private static readonly ConcurrentDictionary<string, LessonBoardState> States = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ClassroomPeer>> PeersByLesson =
        new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> ConnectionToLesson =
        new(StringComparer.Ordinal);

    public async Task JoinLesson(Guid lessonId, string? displayName = null, string? role = null)
    {
        var group = GroupName(lessonId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        ConnectionToLesson[Context.ConnectionId] = group;

        var name = string.IsNullOrWhiteSpace(displayName)
            ? Context.User?.Identity?.Name ?? "Participant"
            : displayName.Trim();
        var peerRole = string.IsNullOrWhiteSpace(role) ? "Participant" : role.Trim();

        var peers = PeersByLesson.GetOrAdd(group, _ => new ConcurrentDictionary<string, ClassroomPeer>(StringComparer.Ordinal));
        var peer = new ClassroomPeer(
            Context.ConnectionId,
            Context.UserIdentifier,
            name,
            peerRole,
            MicOn: true,
            CamOn: false);
        peers[Context.ConnectionId] = peer;

        var existing = peers.Values
            .Where(p => p.ConnectionId != Context.ConnectionId)
            .Select(ToDto)
            .ToList();

        await Clients.Caller.SendAsync("PeerList", lessonId, existing);
        await Clients.OthersInGroup(group).SendAsync("PeerJoined", lessonId, ToDto(peer));

        if (States.TryGetValue(group, out var state) && !string.IsNullOrEmpty(state.BackgroundDocumentId))
        {
            await Clients.Caller.SendAsync(
                "BoardBackgroundChanged",
                new BoardBackgroundDto(lessonId, state.BackgroundDocumentId, state.BackgroundContentType));
        }
    }

    public async Task LeaveLesson(Guid lessonId)
    {
        var group = GroupName(lessonId);
        await RemovePeerFromLessonAsync(Context.ConnectionId, group, lessonId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

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

    /// <summary>WebRTC SDP / ICE relay to a specific peer in the lesson.</summary>
    public async Task SendRtcSignal(Guid lessonId, string targetConnectionId, string type, string payload)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId) || string.IsNullOrWhiteSpace(type))
            return;

        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.ContainsKey(Context.ConnectionId)
            || !peers.ContainsKey(targetConnectionId))
            return;

        await Clients.Client(targetConnectionId).SendAsync(
            "RtcSignal",
            lessonId,
            Context.ConnectionId,
            type,
            payload ?? "");
    }

    public async Task BroadcastMediaState(Guid lessonId, bool micOn, bool camOn)
    {
        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return;

        var updated = peer with { MicOn = micOn, CamOn = camOn };
        peers[Context.ConnectionId] = updated;
        await Clients.OthersInGroup(group).SendAsync(
            "PeerMediaState",
            lessonId,
            Context.ConnectionId,
            micOn,
            camOn);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToLesson.TryRemove(Context.ConnectionId, out var group)
            && TryParseLessonId(group, out var lessonId))
        {
            await RemovePeerFromLessonAsync(Context.ConnectionId, group, lessonId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static bool TryParseLessonId(string group, out Guid lessonId)
    {
        const string prefix = "lesson:";
        lessonId = default;
        return group.StartsWith(prefix, StringComparison.Ordinal)
               && Guid.TryParse(group.AsSpan(prefix.Length), out lessonId);
    }

    private async Task RemovePeerFromLessonAsync(string connectionId, string group, Guid lessonId)
    {
        ConnectionToLesson.TryRemove(connectionId, out _);
        if (!PeersByLesson.TryGetValue(group, out var peers))
            return;

        if (peers.TryRemove(connectionId, out _))
        {
            await Clients.OthersInGroup(group).SendAsync("PeerLeft", lessonId, connectionId);
            if (peers.IsEmpty)
                PeersByLesson.TryRemove(group, out _);
        }
    }

    private static string GroupName(Guid lessonId) => $"lesson:{lessonId:D}";

    private static ClassroomPeerDto ToDto(ClassroomPeer peer) =>
        new(peer.ConnectionId, peer.UserId, peer.DisplayName, peer.Role, peer.MicOn, peer.CamOn);

    private sealed class LessonBoardState
    {
        public string? BackgroundDocumentId { get; set; }
        public string? BackgroundContentType { get; set; }
    }

    private sealed record ClassroomPeer(
        string ConnectionId,
        string? UserId,
        string DisplayName,
        string Role,
        bool MicOn,
        bool CamOn);
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

public record ClassroomPeerDto(
    string ConnectionId,
    string? UserId,
    string DisplayName,
    string Role,
    bool MicOn,
    bool CamOn);
