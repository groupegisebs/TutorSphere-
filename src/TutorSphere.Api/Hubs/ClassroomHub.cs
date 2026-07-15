using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TutorSphere.Application.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Hubs;

/// <summary>
/// Realtime classroom: whiteboard + presence + WebRTC signaling.
/// Presence / caméra / micro : SignalR (effet immédiat pour tous).
/// Flux A/V : WebRTC mesh, signalé via SendRtcSignal.
/// </summary>
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Student},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class ClassroomHub : Hub
{
    private static readonly ConcurrentDictionary<string, LessonBoardState> States = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ClassroomPeer>> PeersByLesson =
        new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> ConnectionToLesson =
        new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<ClassroomChatMessageDto>> ChatByLesson =
        new(StringComparer.Ordinal);
    private const int MaxChatHistory = 80;

    public async Task JoinLesson(
        Guid lessonId,
        string? displayName = null,
        string? role = null,
        bool micOn = true,
        bool camOn = false)
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
            MicOn: micOn,
            CamOn: camOn);
        peers[Context.ConnectionId] = peer;

        var existing = peers.Values
            .Where(p => p.ConnectionId != Context.ConnectionId)
            .Select(ToDto)
            .ToList();

        // Liste complète des pairs déjà présents (avec leur état cam/micro).
        await Clients.Caller.SendAsync("PeerList", lessonId, existing);
        // Tout le monde voit immédiatement le nouvel arrivant.
        await Clients.OthersInGroup(group).SendAsync("PeerJoined", lessonId, ToDto(peer));
        // Demande aux autres de renvoyer leur état média (au cas où).
        await Clients.OthersInGroup(group).SendAsync("MediaSyncRequest", lessonId);

        // Historique court du chat pour le nouvel arrivant.
        if (ChatByLesson.TryGetValue(group, out var chat) && chat.Count > 0)
            await Clients.Caller.SendAsync("ChatHistory", lessonId, chat.ToList());

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

    /// <summary>Chat salle : diffusion immédiate à tous les participants connectés.</summary>
    public async Task SendChatMessage(Guid lessonId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer))
            return;

        var trimmed = text.Trim();
        if (trimmed.Length > 2000)
            trimmed = trimmed[..2000];

        var msg = new ClassroomChatMessageDto(
            lessonId,
            Context.ConnectionId,
            peer.DisplayName,
            peer.Role,
            trimmed,
            DateTime.UtcNow);

        var history = ChatByLesson.GetOrAdd(group, _ => new ConcurrentQueue<ClassroomChatMessageDto>());
        history.Enqueue(msg);
        while (history.Count > MaxChatHistory)
            history.TryDequeue(out _);

        // Tout le monde (y compris l'émetteur) pour rester synchrone.
        await Clients.Group(group).SendAsync("ChatMessage", msg);
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

    /// <summary>État micro/caméra → effet immédiat chez tous les autres (SignalR).</summary>
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

    /// <summary>
    /// Élève demande à partager écran/tableau. Seuls les tuteurs reçoivent la demande.
    /// </summary>
    public async Task RequestShare(Guid lessonId, string kind)
    {
        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var requester))
            return;

        if (IsTutorRole(requester.Role))
            return; // Le tuteur partage sans validation.

        var shareKind = NormalizeShareKind(kind);
        var dto = new ClassroomShareRequestDto(
            lessonId,
            Context.ConnectionId,
            requester.DisplayName,
            shareKind);

        var tutorIds = peers.Values
            .Where(p => IsTutorRole(p.Role))
            .Select(p => p.ConnectionId)
            .ToList();

        if (tutorIds.Count == 0)
        {
            await Clients.Caller.SendAsync("ShareRejected", lessonId, "Aucun tuteur connecté pour valider.");
            return;
        }

        await Clients.Clients(tutorIds).SendAsync("ShareRequest", dto);
    }

    /// <summary>Tuteur approuve ou refuse une demande de partage élève.</summary>
    /// <remarks>
    /// Tableau élève : visible uniquement par les tuteurs jusqu'à PublishShareToClass.
    /// Écran élève : visible par toute la classe dès l'approbation.
    /// </remarks>
    public async Task RespondShare(Guid lessonId, string requesterConnectionId, bool approved, string? kind = null)
    {
        if (string.IsNullOrWhiteSpace(requesterConnectionId))
            return;

        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var responder)
            || !peers.TryGetValue(requesterConnectionId, out var requester))
            return;

        if (!IsTutorRole(responder.Role))
            return;

        if (!approved)
        {
            await Clients.Client(requesterConnectionId).SendAsync(
                "ShareRejected",
                lessonId,
                "Le tuteur a refusé le partage.");
            return;
        }

        var shareKind = NormalizeShareKind(kind);
        // audience: tutor = privé tuteur ; class = toute la classe
        var audience = shareKind == "whiteboard" ? "tutor" : "class";
        await Clients.Client(requesterConnectionId).SendAsync(
            "ShareApproved",
            lessonId,
            shareKind,
            audience);

        if (shareKind == "whiteboard")
        {
            var tutorIds = peers.Values
                .Where(p => IsTutorRole(p.Role))
                .Select(p => p.ConnectionId)
                .ToList();
            await Clients.Clients(tutorIds).SendAsync(
                "ShareLiveStarted",
                lessonId,
                requesterConnectionId,
                requester.DisplayName,
                shareKind);
            return;
        }

        await Clients.Group(group).SendAsync(
            "ShareLiveStarted",
            lessonId,
            requesterConnectionId,
            requester.DisplayName,
            shareKind);
    }

    /// <summary>
    /// Tuteur relaie le tableau (ou partage) d'un élève déjà validé vers toute la classe.
    /// </summary>
    public async Task PublishShareToClass(Guid lessonId, string sharerConnectionId, string? kind = null)
    {
        if (string.IsNullOrWhiteSpace(sharerConnectionId))
            return;

        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var tutor)
            || !peers.TryGetValue(sharerConnectionId, out var sharer))
            return;

        if (!IsTutorRole(tutor.Role))
            return;

        var shareKind = NormalizeShareKind(kind);
        await Clients.Client(sharerConnectionId).SendAsync(
            "SharePublishedToClass",
            lessonId,
            shareKind);

        // Les autres (y compris tuteurs déjà en vue) reçoivent / rafraîchissent le focus classe.
        await Clients.GroupExcept(group, [sharerConnectionId]).SendAsync(
            "ShareLiveStarted",
            lessonId,
            sharerConnectionId,
            sharer.DisplayName,
            shareKind);
    }

    /// <summary>Fin de partage (tuteur ou élève après diffusion).</summary>
    public async Task NotifyShareEnded(Guid lessonId)
    {
        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.ContainsKey(Context.ConnectionId))
            return;

        await Clients.OthersInGroup(group).SendAsync(
            "ShareLiveEnded",
            lessonId,
            Context.ConnectionId);
    }

    /// <summary>Partage tuteur immédiat — informe la classe pour focus scène / tableau.</summary>
    public async Task AnnounceTutorShare(Guid lessonId, string kind)
    {
        var group = GroupName(lessonId);
        if (!PeersByLesson.TryGetValue(group, out var peers)
            || !peers.TryGetValue(Context.ConnectionId, out var peer)
            || !IsTutorRole(peer.Role))
            return;

        var shareKind = NormalizeShareKind(kind);
        await Clients.OthersInGroup(group).SendAsync(
            "ShareLiveStarted",
            lessonId,
            Context.ConnectionId,
            peer.DisplayName,
            shareKind);
    }

    private static string NormalizeShareKind(string? kind)
    {
        if (string.Equals(kind, "whiteboard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "board", StringComparison.OrdinalIgnoreCase))
            return "whiteboard";
        return "screen";
    }

    private static bool IsTutorRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return role.Equals("Tuteur", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Tutor", StringComparison.OrdinalIgnoreCase)
            || role.Equals("TeachingAssistant", StringComparison.OrdinalIgnoreCase)
            || role.Equals("TA", StringComparison.OrdinalIgnoreCase);
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

        if (peers.TryRemove(connectionId, out var removed))
        {
            // Tout le monde retire immédiatement ce participant.
            await Clients.OthersInGroup(group).SendAsync("PeerLeft", lessonId, connectionId, removed.DisplayName);
            if (peers.IsEmpty)
            {
                PeersByLesson.TryRemove(group, out _);
                ChatByLesson.TryRemove(group, out _);
            }
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

public record ClassroomChatMessageDto(
    Guid LessonId,
    string SenderConnectionId,
    string SenderDisplayName,
    string SenderRole,
    string Text,
    DateTime SentAtUtc);

public record ClassroomShareRequestDto(
    Guid LessonId,
    string RequesterConnectionId,
    string RequesterDisplayName,
    string Kind);
