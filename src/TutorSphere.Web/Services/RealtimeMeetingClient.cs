using Microsoft.AspNetCore.SignalR.Client;

namespace TutorSphere.Web.Services;

public sealed class RealtimeMeetingClient : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RealtimeMeetingClient> _logger;
    private HubConnection? _hub;
    private bool _handlersBound;
    private Guid? _joined;
    private string? _guestToken;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RealtimeMeetingClient(AuthService auth, IHttpClientFactory httpClientFactory, ILogger<RealtimeMeetingClient> logger)
    {
        _auth = auth;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public event Action<Guid, IReadOnlyList<MeetingLivePeerDto>>? PeerListReceived;
    public event Action<Guid, MeetingLivePeerDto>? PeerJoined;
    public event Action<Guid, string>? PeerLeft;
    public event Action<Guid, string, bool, bool>? PeerMediaStateChanged;
    public event Action<Guid, string, string, string>? RtcSignalReceived;
    public event Action<Guid>? MediaSyncRequested;
    public event Action<MeetingLiveChatDto>? ChatMessageReceived;
    public event Action<Guid, IReadOnlyList<MeetingLiveChatDto>>? ChatHistoryReceived;
    public event Action<Guid, IReadOnlyList<MeetingLivePeerDto>>? WaitingUpdated;
    public event Action<Guid>? WaitingRoom;
    public event Action<Guid>? Admitted;
    public event Action<Guid>? Denied;
    public event Action<Guid>? Kicked;
    public event Action<Guid, string, string, bool>? HandRaised;
    public event Action<Guid, string, string, string>? Reaction;
    public event Action<Guid, string, string>? Caption;
    public event Action<Guid>? ForceMute;
    public event Action<Guid>? ForceStopShare;
    public event Action<Guid, bool>? MeetingLocked;
    public event Action<Guid>? AiEnabled;
    public event Action<Guid, string, bool>? AiConsent;
    public event Action<Guid>? MeetingEnded;
    public event Action<Guid, bool>? RecordingState;
    public event Action<Guid, string>? SharedNotes;
    public event Action<Guid, string, string[]>? Poll;
    public event Action<Guid, string, int>? PollVote;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public string? ConnectionId => _hub?.ConnectionId;

    public Task EnsureHubReadyAsync(string? guestToken = null)
    {
        _guestToken = guestToken;
        return EnsureConnectedAsync();
    }

    public async Task JoinAsync(
        Guid meetingId, string displayName, string role, bool micOn, bool camOn,
        string? guestToken = null, string? accessCode = null)
    {
        await EnsureConnectedAsync();
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        if (_joined is Guid prev && prev != meetingId)
        {
            try { await _hub.InvokeAsync("LeaveMeeting", prev); } catch { }
        }
        await _hub.InvokeAsync("JoinMeeting", meetingId, displayName, role, micOn, camOn, guestToken, accessCode);
        _joined = meetingId;
    }

    public async Task LeaveAsync()
    {
        if (_hub is null || _joined is null) return;
        try { await _hub.InvokeAsync("LeaveMeeting", _joined.Value); } catch { }
        _joined = null;
    }

    public Task SendChatAsync(Guid id, string text) => Send("SendChatMessage", id, text);
    public Task SendRtcAsync(Guid id, string target, string type, string payload) => Send("SendRtcSignal", id, target, type, payload);
    public Task BroadcastMediaAsync(Guid id, bool mic, bool cam) => Send("BroadcastMediaState", id, mic, cam);
    public Task RaiseHandAsync(Guid id, bool raised) => Send("RaiseHand", id, raised);
    public Task ReactAsync(Guid id, string emoji) => Send("SendReaction", id, emoji);
    public Task CaptionAsync(Guid id, string text) => Send("SendCaption", id, text);
    public Task ForceMuteAsync(Guid id, string target) => Send("ForceMute", id, target);
    public Task StopShareAsync(Guid id, string target) => Send("StopShare", id, target);
    public Task KickAsync(Guid id, string target) => Send("KickPeer", id, target);
    public Task RequestWaitingListAsync(Guid id) => Send("RequestWaitingList", id);
    public Task AdmitAsync(Guid id, string connectionId) => Send("AdmitWaiting", id, connectionId);
    public Task DenyAsync(Guid id, string connectionId) => Send("DenyWaiting", id, connectionId);
    public Task NotifyLockedAsync(Guid id, bool locked) => Send("NotifyLocked", id, locked);
    public Task NotifyAiAsync(Guid id) => Send("NotifyAiEnabled", id);
    public Task NotifyConsentAsync(Guid id, bool ok) => Send("NotifyAiConsent", id, ok);
    public Task NotifyEndedAsync(Guid id) => Send("NotifyEnded", id);
    public Task NotifyRecordingAsync(Guid id, bool on) => Send("NotifyRecording", id, on);
    public Task BroadcastNotesAsync(Guid id, string notes) => Send("BroadcastNotes", id, notes);
    public Task BroadcastPollAsync(Guid id, string q, string[] options) => Send("BroadcastPoll", id, q, options);
    public Task VotePollAsync(Guid id, int index) => Send("VotePoll", id, index);

    private async Task Send(string method, params object?[] args)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.SendCoreAsync(method, args!); }
        catch (Exception ex) { _logger.LogDebug(ex, "{Method} failed", method); }
    }

    private async Task EnsureConnectedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(_guestToken))
                await _auth.EnsureSessionRestoredAsync();

            if (_hub is { State: HubConnectionState.Connected }) return;

            if (_hub is not null)
            {
                await _hub.DisposeAsync();
                _hub = null;
                _handlersBound = false;
            }

            var apiClient = _httpClientFactory.CreateClient("TutorSphereApi");
            var baseUri = apiClient.BaseAddress
                ?? throw new InvalidOperationException("TutorSphereApi BaseAddress manquant.");
            var hubUri = new Uri(baseUri, "hubs/meeting");

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUri, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(
                        string.IsNullOrEmpty(_guestToken) ? _auth.Token : null);
                })
                .WithAutomaticReconnect()
                .Build();

            BindHandlers(_hub);
            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de connecter le hub réunion.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void BindHandlers(HubConnection hub)
    {
        if (_handlersBound) return;
        _handlersBound = true;
        hub.On<Guid, List<MeetingLivePeerDto>>("PeerList", (id, peers) => PeerListReceived?.Invoke(id, peers ?? []));
        hub.On<Guid, MeetingLivePeerDto>("PeerJoined", (id, peer) => PeerJoined?.Invoke(id, peer));
        hub.On<Guid, string, string>("PeerLeft", (id, cid, _) => PeerLeft?.Invoke(id, cid));
        hub.On<Guid, string, bool, bool>("PeerMediaState", (id, cid, mic, cam) => PeerMediaStateChanged?.Invoke(id, cid, mic, cam));
        hub.On<Guid, string, string, string>("RtcSignal", (id, from, type, payload) => RtcSignalReceived?.Invoke(id, from, type, payload));
        hub.On<Guid>("MediaSyncRequest", id => MediaSyncRequested?.Invoke(id));
        hub.On<MeetingLiveChatDto>("ChatMessage", msg => ChatMessageReceived?.Invoke(msg));
        hub.On<Guid, List<MeetingLiveChatDto>>("ChatHistory", (id, list) => ChatHistoryReceived?.Invoke(id, list ?? []));
        hub.On<Guid, List<MeetingLivePeerDto>>("WaitingUpdated", (id, list) => WaitingUpdated?.Invoke(id, list ?? []));
        hub.On<Guid>("WaitingRoom", id => WaitingRoom?.Invoke(id));
        hub.On<Guid>("Admitted", id => Admitted?.Invoke(id));
        hub.On<Guid>("Denied", id => Denied?.Invoke(id));
        hub.On<Guid>("Kicked", id => Kicked?.Invoke(id));
        hub.On<Guid, string, string, bool>("HandRaised", (id, cid, name, raised) => HandRaised?.Invoke(id, cid, name, raised));
        hub.On<Guid, string, string, string>("Reaction", (id, cid, name, emoji) => Reaction?.Invoke(id, cid, name, emoji));
        hub.On<Guid, string, string>("Caption", (id, name, text) => Caption?.Invoke(id, name, text));
        hub.On<Guid>("ForceMute", id => ForceMute?.Invoke(id));
        hub.On<Guid>("ForceStopShare", id => ForceStopShare?.Invoke(id));
        hub.On<Guid, bool>("MeetingLocked", (id, locked) => MeetingLocked?.Invoke(id, locked));
        hub.On<Guid>("AiEnabled", id => AiEnabled?.Invoke(id));
        hub.On<Guid, string, bool>("AiConsent", (id, name, ok) => AiConsent?.Invoke(id, name, ok));
        hub.On<Guid>("MeetingEnded", id => MeetingEnded?.Invoke(id));
        hub.On<Guid, bool>("RecordingState", (id, on) => RecordingState?.Invoke(id, on));
        hub.On<Guid, string>("SharedNotes", (id, notes) => SharedNotes?.Invoke(id, notes));
        hub.On<Guid, string, string[]>("Poll", (id, q, opts) => Poll?.Invoke(id, q, opts ?? []));
        hub.On<Guid, string, int>("PollVote", (id, name, i) => PollVote?.Invoke(id, name, i));
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveAsync();
        if (_hub is not null) await _hub.DisposeAsync();
        _gate.Dispose();
    }
}

public sealed record MeetingLivePeerDto(
    string ConnectionId,
    string? UserId,
    string DisplayName,
    string Role,
    bool MicOn,
    bool CamOn,
    bool Waiting,
    bool HandRaised);

public sealed record MeetingLiveChatDto(
    Guid MeetingId,
    string ConnectionId,
    string SenderName,
    string Role,
    string Text,
    DateTime SentAtUtc);
