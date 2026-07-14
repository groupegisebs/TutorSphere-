using Microsoft.AspNetCore.SignalR.Client;

namespace TutorSphere.Web.Services;

/// <summary>Circuit-scoped SignalR client for collaborative classroom (whiteboard + WebRTC signaling).</summary>
public sealed class RealtimeClassroomClient : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RealtimeClassroomClient> _logger;
    private HubConnection? _hub;
    private bool _handlersBound;
    private Guid? _joinedLessonId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RealtimeClassroomClient(
        AuthService auth,
        IHttpClientFactory httpClientFactory,
        ILogger<RealtimeClassroomClient> logger)
    {
        _auth = auth;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public event Action<BoardStrokeDto>? StrokeReceived;
    public event Action? BoardCleared;
    public event Action<BoardBackgroundDto>? BackgroundChanged;
    public event Action<Guid, IReadOnlyList<ClassroomPeerDto>>? PeerListReceived;
    public event Action<Guid, ClassroomPeerDto>? PeerJoined;
    public event Action<Guid, string>? PeerLeft;
    public event Action<Guid, string, bool, bool>? PeerMediaStateChanged;
    public event Action<Guid, string, string, string>? RtcSignalReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    /// <summary>SignalR connection id for this browser tab (used for WebRTC politeness).</summary>
    public string? ConnectionId => _hub?.ConnectionId;

    /// <summary>Ensure SignalR hub is connected so <see cref="ConnectionId"/> is available before JoinLesson.</summary>
    public Task EnsureHubReadyAsync() => EnsureConnectedAsync();

    public async Task JoinLessonAsync(Guid lessonId, string? displayName = null, string? role = null)
    {
        await EnsureConnectedAsync();
        if (_hub is null || _hub.State != HubConnectionState.Connected)
            return;

        if (_joinedLessonId is Guid prev && prev != lessonId)
        {
            try { await _hub.InvokeAsync("LeaveLesson", prev); }
            catch { /* ignore */ }
        }

        await _hub.InvokeAsync("JoinLesson", lessonId, displayName, role);
        _joinedLessonId = lessonId;
    }

    public async Task SendStrokeAsync(Guid lessonId, BoardStrokeDto stroke)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("SendStroke", lessonId, stroke); }
        catch (Exception ex) { _logger.LogDebug(ex, "SendStroke failed"); }
    }

    public async Task ClearBoardAsync(Guid lessonId)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("ClearBoard", lessonId); }
        catch (Exception ex) { _logger.LogDebug(ex, "ClearBoard failed"); }
    }

    public async Task SetBackgroundAsync(Guid lessonId, string? documentId, string? contentType)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("SetBackground", lessonId, documentId, contentType); }
        catch (Exception ex) { _logger.LogDebug(ex, "SetBackground failed"); }
    }

    public async Task SendRtcSignalAsync(Guid lessonId, string targetConnectionId, string type, string payload)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("SendRtcSignal", lessonId, targetConnectionId, type, payload); }
        catch (Exception ex) { _logger.LogDebug(ex, "SendRtcSignal failed"); }
    }

    public async Task BroadcastMediaStateAsync(Guid lessonId, bool micOn, bool camOn)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("BroadcastMediaState", lessonId, micOn, camOn); }
        catch (Exception ex) { _logger.LogDebug(ex, "BroadcastMediaState failed"); }
    }

    public async Task LeaveAsync()
    {
        if (_hub is null || _joinedLessonId is null) return;
        try { await _hub.InvokeAsync("LeaveLesson", _joinedLessonId.Value); }
        catch { /* ignore */ }
        _joinedLessonId = null;
    }

    private async Task EnsureConnectedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await _auth.EnsureSessionRestoredAsync();
            if (string.IsNullOrEmpty(_auth.Token))
                return;

            if (_hub is { State: HubConnectionState.Connected or HubConnectionState.Connecting })
                return;

            if (_hub is not null)
            {
                await _hub.DisposeAsync();
                _hub = null;
                _handlersBound = false;
            }

            var apiClient = _httpClientFactory.CreateClient("TutorSphereApi");
            var baseUri = apiClient.BaseAddress
                ?? throw new InvalidOperationException("TutorSphereApi BaseAddress manquant.");
            var hubUri = new Uri(baseUri, "hubs/classroom");

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUri, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_auth.Token);
                })
                .WithAutomaticReconnect()
                .Build();

            BindHandlers(_hub);
            await _hub.StartAsync();
            _logger.LogDebug("SignalR classroom hub connected.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de connecter le hub SignalR classroom.");
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

        hub.On<BoardStrokeDto>("BoardStroke", stroke =>
        {
            try { StrokeReceived?.Invoke(stroke); }
            catch (Exception ex) { _logger.LogWarning(ex, "BoardStroke handler error"); }
        });

        hub.On<Guid>("BoardCleared", _ =>
        {
            try { BoardCleared?.Invoke(); }
            catch (Exception ex) { _logger.LogWarning(ex, "BoardCleared handler error"); }
        });

        hub.On<BoardBackgroundDto>("BoardBackgroundChanged", bg =>
        {
            try { BackgroundChanged?.Invoke(bg); }
            catch (Exception ex) { _logger.LogWarning(ex, "BoardBackgroundChanged handler error"); }
        });

        hub.On<Guid, List<ClassroomPeerDto>>("PeerList", (lessonId, peers) =>
        {
            try { PeerListReceived?.Invoke(lessonId, peers ?? []); }
            catch (Exception ex) { _logger.LogWarning(ex, "PeerList handler error"); }
        });

        hub.On<Guid, ClassroomPeerDto>("PeerJoined", (lessonId, peer) =>
        {
            try { PeerJoined?.Invoke(lessonId, peer); }
            catch (Exception ex) { _logger.LogWarning(ex, "PeerJoined handler error"); }
        });

        hub.On<Guid, string>("PeerLeft", (lessonId, connectionId) =>
        {
            try { PeerLeft?.Invoke(lessonId, connectionId); }
            catch (Exception ex) { _logger.LogWarning(ex, "PeerLeft handler error"); }
        });

        hub.On<Guid, string, bool, bool>("PeerMediaState", (lessonId, connectionId, micOn, camOn) =>
        {
            try { PeerMediaStateChanged?.Invoke(lessonId, connectionId, micOn, camOn); }
            catch (Exception ex) { _logger.LogWarning(ex, "PeerMediaState handler error"); }
        });

        hub.On<Guid, string, string, string>("RtcSignal", (lessonId, fromConnectionId, type, payload) =>
        {
            try { RtcSignalReceived?.Invoke(lessonId, fromConnectionId, type, payload); }
            catch (Exception ex) { _logger.LogWarning(ex, "RtcSignal handler error"); }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveAsync();
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
        _gate.Dispose();
    }
}

// Mirror DTOs on the Web side so the project does not reference the Api assembly.
public record BoardStrokeDto(
    string Phase,
    double X,
    double Y,
    string Tool,
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
