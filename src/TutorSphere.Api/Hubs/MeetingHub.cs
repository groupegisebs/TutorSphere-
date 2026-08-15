using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application.Services;

namespace TutorSphere.Api.Hubs;

/// <summary>
/// Salle de réunion : présence, chat, WebRTC, salle d’attente, réactions, IA.
/// </summary>
[AllowAnonymous]
public class MeetingHub(IServiceScopeFactory scopes) : Hub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MeetingPeer>> PeersByMeeting = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> ConnectionToMeeting = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<MeetingChatDto>> ChatByMeeting = new(StringComparer.Ordinal);
    private const int MaxChatHistory = 120;

    public async Task JoinMeeting(
        Guid meetingId,
        string? displayName = null,
        string? role = null,
        bool micOn = true,
        bool camOn = false,
        string? guestToken = null)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        await using (var scope = scopes.CreateAsyncScope())
        {
            var meetings = scope.ServiceProvider.GetRequiredService<IExpertMeetingService>();
            await meetings.EnsureCanJoinLiveAsync(userId, meetingId, guestToken, Context.ConnectionAborted);
        }

        var group = GroupName(meetingId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        ConnectionToMeeting[Context.ConnectionId] = group;

        var name = string.IsNullOrWhiteSpace(displayName)
            ? Context.User?.Identity?.Name ?? "Participant"
            : displayName.Trim();
        var peerRole = string.IsNullOrWhiteSpace(role) ? "Participant" : role.Trim();
        var waiting = string.Equals(peerRole, "Waiting", StringComparison.OrdinalIgnoreCase);

        var peers = PeersByMeeting.GetOrAdd(group, _ => new ConcurrentDictionary<string, MeetingPeer>(StringComparer.Ordinal));
        var peer = new MeetingPeer(Context.ConnectionId, userId, name, peerRole, micOn, camOn, waiting, guestToken);
        peers[Context.ConnectionId] = peer;

        if (waiting)
        {
            await Clients.Group(group).SendAsync("WaitingUpdated", meetingId, WaitingList(peers));
            await Clients.Caller.SendAsync("WaitingRoom", meetingId);
            return;
        }

        var existing = peers.Values
            .Where(p => p.ConnectionId != Context.ConnectionId && !p.Waiting)
            .Select(ToDto)
            .ToList();
        await Clients.Caller.SendAsync("PeerList", meetingId, existing);
        await Clients.OthersInGroup(group).SendAsync("PeerJoined", meetingId, ToDto(peer));
        await Clients.OthersInGroup(group).SendAsync("MediaSyncRequest", meetingId);
        await Clients.Group(group).SendAsync("WaitingUpdated", meetingId, WaitingList(peers));

        if (ChatByMeeting.TryGetValue(group, out var chat) && chat.Count > 0)
            await Clients.Caller.SendAsync("ChatHistory", meetingId, chat.ToList());
    }

    public async Task LeaveMeeting(Guid meetingId)
    {
        await RemovePeerAsync(Context.ConnectionId, GroupName(meetingId), meetingId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(meetingId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToMeeting.TryRemove(Context.ConnectionId, out var group)
            && Guid.TryParse(group["meet:".Length..], out var meetingId))
            await RemovePeerAsync(Context.ConnectionId, group, meetingId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendChatMessage(Guid meetingId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer)
            || peer.Waiting)
            return;

        var trimmed = text.Trim();
        if (trimmed.Length > 2000) trimmed = trimmed[..2000];
        var msg = new MeetingChatDto(meetingId, Context.ConnectionId, peer.DisplayName, peer.Role, trimmed, DateTime.UtcNow);
        var history = ChatByMeeting.GetOrAdd(group, _ => new ConcurrentQueue<MeetingChatDto>());
        history.Enqueue(msg);
        while (history.Count > MaxChatHistory) history.TryDequeue(out _);
        await Clients.Group(group).SendAsync("ChatMessage", msg);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var svc = scope.ServiceProvider.GetRequiredService<IExpertMeetingService>();
            await svc.PersistChatAsync(meetingId, peer.UserId ?? peer.ConnectionId, peer.DisplayName, trimmed, Context.ConnectionAborted);
        }
        catch { /* non bloquant */ }
    }

    public async Task SendRtcSignal(Guid meetingId, string targetConnectionId, string type, string payload)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId) || string.IsNullOrWhiteSpace(type))
            return;
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.ContainsKey(Context.ConnectionId)
            || !peers.ContainsKey(targetConnectionId)
            || peers[Context.ConnectionId].Waiting
            || peers[targetConnectionId].Waiting)
            return;
        await Clients.Client(targetConnectionId).SendAsync("RtcSignal", meetingId, Context.ConnectionId, type, payload ?? "");
    }

    public async Task BroadcastMediaState(Guid meetingId, bool micOn, bool camOn)
    {
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return;
        peers[Context.ConnectionId] = peer with { MicOn = micOn, CamOn = camOn };
        await Clients.OthersInGroup(group).SendAsync("PeerMediaState", meetingId, Context.ConnectionId, micOn, camOn);
    }

    public async Task RaiseHand(Guid meetingId, bool raised)
    {
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer)
            || peer.Waiting)
            return;
        peers[Context.ConnectionId] = peer with { HandRaised = raised };
        await Clients.Group(group).SendAsync("HandRaised", meetingId, Context.ConnectionId, peer.DisplayName, raised);
    }

    public async Task SendReaction(Guid meetingId, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji)) return;
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer)
            || peer.Waiting)
            return;
        await Clients.Group(group).SendAsync("Reaction", meetingId, Context.ConnectionId, peer.DisplayName, emoji.Trim()[..Math.Min(8, emoji.Trim().Length)]);
    }

    public async Task SendCaption(Guid meetingId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer)
            || peer.Waiting)
            return;
        var chunk = text.Trim();
        if (chunk.Length > 400) chunk = chunk[..400];
        await Clients.Group(group).SendAsync("Caption", meetingId, peer.DisplayName, chunk);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var svc = scope.ServiceProvider.GetRequiredService<IExpertMeetingService>();
            await svc.AppendTranscriptAsync(meetingId, $"{peer.DisplayName}: {chunk}", Context.ConnectionAborted);
        }
        catch { /* non bloquant */ }
    }

    public async Task ForceMute(Guid meetingId, string targetConnectionId)
    {
        if (!IsModerator()) return;
        await Clients.Client(targetConnectionId).SendAsync("ForceMute", meetingId);
        await Clients.Group(GroupName(meetingId)).SendAsync("PeerMediaState", meetingId, targetConnectionId, false, true);
    }

    public async Task StopShare(Guid meetingId, string targetConnectionId)
    {
        if (!IsModerator()) return;
        await Clients.Client(targetConnectionId).SendAsync("ForceStopShare", meetingId);
        await Clients.Group(GroupName(meetingId)).SendAsync("ShareLiveEnded", meetingId, targetConnectionId);
    }

    public async Task KickPeer(Guid meetingId, string targetConnectionId)
    {
        if (!IsModerator()) return;
        await Clients.Client(targetConnectionId).SendAsync("Kicked", meetingId);
        await RemovePeerAsync(targetConnectionId, GroupName(meetingId), meetingId);
    }

    public async Task AdmitWaiting(Guid meetingId, string connectionId)
    {
        if (!IsModerator()) return;
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers) || !peers.TryGetValue(connectionId, out var peer))
            return;
        var admitted = peer with { Waiting = false, Role = "Participant" };
        peers[connectionId] = admitted;
        await Groups.AddToGroupAsync(connectionId, group);
        await Clients.Client(connectionId).SendAsync("Admitted", meetingId);
        await Clients.Group(group).SendAsync("PeerJoined", meetingId, ToDto(admitted));
        await Clients.Group(group).SendAsync("WaitingUpdated", meetingId, WaitingList(peers));
    }

    public async Task DenyWaiting(Guid meetingId, string connectionId)
    {
        if (!IsModerator()) return;
        await Clients.Client(connectionId).SendAsync("Denied", meetingId);
        await RemovePeerAsync(connectionId, GroupName(meetingId), meetingId);
    }

    public async Task NotifyLocked(Guid meetingId, bool locked)
    {
        if (!IsModerator()) return;
        await Clients.Group(GroupName(meetingId)).SendAsync("MeetingLocked", meetingId, locked);
    }

    public async Task NotifyAiEnabled(Guid meetingId)
    {
        if (!IsModerator()) return;
        await Clients.Group(GroupName(meetingId)).SendAsync("AiEnabled", meetingId);
    }

    public async Task NotifyAiConsent(Guid meetingId, bool consented)
    {
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return;
        await Clients.Group(group).SendAsync("AiConsent", meetingId, peer.DisplayName, consented);
    }

    public async Task NotifyEnded(Guid meetingId)
    {
        if (!IsModerator()) return;
        await Clients.Group(GroupName(meetingId)).SendAsync("MeetingEnded", meetingId);
    }

    public async Task NotifyRecording(Guid meetingId, bool on)
    {
        if (!IsModerator()) return;
        await Clients.Group(GroupName(meetingId)).SendAsync("RecordingState", meetingId, on);
    }

    public async Task BroadcastNotes(Guid meetingId, string notes)
    {
        await Clients.OthersInGroup(GroupName(meetingId)).SendAsync("SharedNotes", meetingId, notes ?? "");
    }

    public async Task BroadcastPoll(Guid meetingId, string question, string[] options)
    {
        if (!IsModerator()) return;
        await Clients.Group(GroupName(meetingId)).SendAsync("Poll", meetingId, question, options);
    }

    public async Task VotePoll(Guid meetingId, int optionIndex)
    {
        var group = GroupName(meetingId);
        if (!PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return;
        await Clients.Group(group).SendAsync("PollVote", meetingId, peer.DisplayName, optionIndex);
    }

    private bool IsModerator()
    {
        if (!ConnectionToMeeting.TryGetValue(Context.ConnectionId, out var group)
            || !PeersByMeeting.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return false;
        return peer.Role is "Organizer" or "CoOrganizer" or "Organisateur" or "Co-organisateur";
    }

    private async Task RemovePeerAsync(string connectionId, string group, Guid meetingId)
    {
        if (PeersByMeeting.TryGetValue(group, out var peers) && peers.TryRemove(connectionId, out var peer))
        {
            await Clients.Group(group).SendAsync("PeerLeft", meetingId, connectionId, peer.DisplayName);
            await Clients.Group(group).SendAsync("WaitingUpdated", meetingId, WaitingList(peers));
        }
        ConnectionToMeeting.TryRemove(connectionId, out _);
    }

    private static IReadOnlyList<MeetingPeerDto> WaitingList(ConcurrentDictionary<string, MeetingPeer> peers) =>
        peers.Values.Where(p => p.Waiting).Select(ToDto).ToList();

    private static string GroupName(Guid meetingId) => $"meet:{meetingId:D}";
    private static MeetingPeerDto ToDto(MeetingPeer p) =>
        new(p.ConnectionId, p.UserId, p.DisplayName, p.Role, p.MicOn, p.CamOn, p.Waiting, p.HandRaised);
}

public sealed record MeetingPeer(
    string ConnectionId,
    string? UserId,
    string DisplayName,
    string Role,
    bool MicOn,
    bool CamOn,
    bool Waiting,
    string? GuestToken = null,
    bool HandRaised = false);

public sealed record MeetingPeerDto(
    string ConnectionId,
    string? UserId,
    string DisplayName,
    string Role,
    bool MicOn,
    bool CamOn,
    bool Waiting,
    bool HandRaised);

public sealed record MeetingChatDto(
    Guid MeetingId,
    string ConnectionId,
    string SenderName,
    string Role,
    string Text,
    DateTime SentAtUtc);
