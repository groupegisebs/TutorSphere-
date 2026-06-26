using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TutorSphere.Infrastructure.Email;

public sealed class MailGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly MailGatewaySettings _settings;
    private readonly ILogger<MailGatewayClient> _logger;

    public MailGatewayClient(
        HttpClient http,
        IOptions<MailGatewaySettings> settings,
        ILogger<MailGatewayClient> logger)
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

    public async Task<SendMailResponse> SendAsync(SendMailRequest request, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/mail/send");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        _logger.LogDebug("MailGateway → template={Template} to={To}", request.TemplateCode, string.Join(",", request.To));

        using var response = await _http.SendAsync(httpRequest, ct);
        var payload = await response.Content.ReadFromJsonAsync<SendMailResponse>(JsonOptions, ct);

        if (payload is null)
            throw new InvalidOperationException("Réponse vide du service d'envoi d'e-mails.");

        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            _logger.LogWarning("MailGateway HTTP {Status}: {Error}", (int)response.StatusCode, payload.Error);
        }

        return payload;
    }
}
