using Microsoft.AspNetCore.SignalR.Client;
using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Web.Services;

/// <summary>
/// Circuit-scoped SignalR client for /hubs/messages.
/// Receives <c>ReceiveMessage</c> pushed by the API after each send.
/// </summary>
public sealed class RealtimeMessagingClient : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MessagingNotificationState _notifications;
    private readonly MessageService _messages;
    private readonly ILogger<RealtimeMessagingClient> _logger;
    private HubConnection? _hub;
    private bool _handlersBound;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RealtimeMessagingClient(
        AuthService auth,
        IHttpClientFactory httpClientFactory,
        MessagingNotificationState notifications,
        MessageService messages,
        ILogger<RealtimeMessagingClient> logger)
    {
        _auth = auth;
        _httpClientFactory = httpClientFactory;
        _notifications = notifications;
        _messages = messages;
        _logger = logger;
    }

    public event Action<MessageDto>? MessageReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task EnsureConnectedAsync()
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
            var hubUri = new Uri(baseUri, "hubs/messages");

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUri, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_auth.Token);
                })
                .WithAutomaticReconnect()
                .Build();

            BindHandlers(_hub);
            await _hub.StartAsync();
            await SeedUnreadAsync();
            _logger.LogDebug("SignalR messages hub connected.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de connecter le hub SignalR messages.");
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

        hub.On<MessageDto>("ReceiveMessage", message =>
        {
            try
            {
                _notifications.HandleIncoming(message);
                MessageReceived?.Invoke(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors du traitement ReceiveMessage.");
            }
        });

        hub.Reconnected += async _ =>
        {
            try { await SeedUnreadAsync(); }
            catch { /* ignore */ }
        };
    }

    private async Task SeedUnreadAsync()
    {
        try
        {
            var conversations = await _messages.GetConversationsAsync();
            _notifications.SetUnreadTotal(conversations.Sum(c => c.UnreadCount));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Seed unread messages failed.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            try { await _hub.DisposeAsync(); }
            catch { /* ignore */ }
            _hub = null;
        }

        _gate.Dispose();
    }
}
