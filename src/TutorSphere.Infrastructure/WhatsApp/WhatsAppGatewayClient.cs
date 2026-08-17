using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Infrastructure.Email;

namespace TutorSphere.Infrastructure.WhatsApp;

/// <summary>
/// Client du canal WhatsApp de Mail Sender. Réutilise la configuration du courriel
/// (<see cref="MailGatewaySettings"/>) : même passerelle, même clé, même code client.
/// </summary>
public sealed class WhatsAppGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly MailGatewaySettings _settings;
    private readonly ILogger<WhatsAppGatewayClient> _logger;

    public WhatsAppGatewayClient(
        HttpClient http,
        IOptions<MailGatewaySettings> settings,
        ILogger<WhatsAppGatewayClient> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_settings.BaseUrl);

    public string ClientCode => _settings.ClientCode;

    public async Task<SendWhatsAppResponse> SendTemplateAsync(
        string to,
        string templateCode,
        Dictionary<string, string> bodyData,
        string? language,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new SendWhatsAppResponse(false, null, null, null, to, "Canal WhatsApp non configuré côté TutorSphere.");

        var request = new SendWhatsAppRequest(
            ClientCode: _settings.ClientCode,
            To: to,
            TemplateCode: templateCode,
            BodyData: bodyData,
            Language: language);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/whatsapp/send");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        try
        {
            using var response = await _http.SendAsync(httpRequest, ct);
            var payload = await ReadPayloadAsync(response, ct);

            if (payload is null)
            {
                return new SendWhatsAppResponse(
                    false, null, null, null, to,
                    $"Réponse illisible de Mail Sender (HTTP {(int)response.StatusCode}).");
            }

            if (!response.IsSuccessStatusCode || !payload.Success)
            {
                _logger.LogWarning(
                    "WhatsApp HTTP {Status}: {Error} (template={Template})",
                    (int)response.StatusCode, payload.Error, templateCode);
                return payload with { Success = false };
            }

            _logger.LogInformation(
                "WhatsApp OK template={Template} tracking={TrackingId}",
                templateCode, payload.TrackingId);

            return payload;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Passerelle injoignable : la notification WhatsApp est accessoire, le courriel est déjà parti.
            _logger.LogWarning(ex, "Mail Sender injoignable pour un envoi WhatsApp (template={Template})", templateCode);
            return new SendWhatsAppResponse(false, null, null, null, to, "Passerelle injoignable.");
        }
    }

    private static async Task<SendWhatsAppResponse?> ReadPayloadAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<SendWhatsAppResponse>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Corps non JSON : page d'erreur d'un proxy, ou 401 renvoyé par le middleware de clé API.
            return null;
        }
    }
}
