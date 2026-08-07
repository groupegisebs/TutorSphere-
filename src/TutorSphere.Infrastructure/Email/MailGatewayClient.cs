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

        _logger.LogInformation(
            "Mail Sender → POST api/mail/send template={Template} lang={Language} client={Client} to={To}",
            request.TemplateCode,
            request.Language ?? "fr",
            request.ClientCode,
            string.Join(",", request.To));

        using var response = await _http.SendAsync(httpRequest, ct);
        var payload = await response.Content.ReadFromJsonAsync<SendMailResponse>(JsonOptions, ct);

        if (payload is null)
            throw new InvalidOperationException("Réponse vide de Mail Sender (GiseMailSender).");

        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            _logger.LogWarning(
                "Mail Sender HTTP {Status}: {Error} (template={Template})",
                (int)response.StatusCode,
                payload.Error,
                request.TemplateCode);
        }
        else
        {
            _logger.LogInformation(
                "Mail Sender OK template={Template} tracking={TrackingId}",
                request.TemplateCode,
                payload.TrackingId);
        }

        return payload;
    }
}
