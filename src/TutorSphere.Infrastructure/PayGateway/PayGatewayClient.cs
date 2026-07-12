using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TutorSphere.Infrastructure.PayGateway;

internal sealed class PayGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly PayGatewaySettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PayGatewayClient> _logger;

    public PayGatewayClient(
        HttpClient http,
        IOptions<PayGatewaySettings> settings,
        IHostEnvironment environment,
        ILogger<PayGatewayClient> logger)
    {
        _http = http;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<GatewayCheckoutSessionResponse> CreateCheckoutSessionAsync(
        GatewayCheckoutSessionRequest request,
        CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/checkout/session", request, ct);
        return await ReadSuccessAsync<GatewayCheckoutSessionResponse>(response, ct);
    }

    public async Task<GatewayPaymentResponse?> GetPaymentAsync(string paymentCode, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/payments/{Uri.EscapeDataString(paymentCode)}", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await ReadSuccessAsync<GatewayPaymentResponse>(response, ct);
    }

    public async Task<GatewayProductResponse?> GetProductAsync(string productCode, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/products/{Uri.EscapeDataString(productCode)}", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await ReadSuccessAsync<GatewayProductResponse>(response, ct);
    }

    public async Task CreateCatalogItemAsync(GatewayCreateCatalogItemRequest request, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/products/catalog", request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<GatewayApiSubscriptionResponse>> GetCustomerSubscriptionsAsync(
        string customerCode,
        CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"api/customers/{Uri.EscapeDataString(customerCode)}/subscriptions",
            null,
            ct);

        return await ReadSuccessAsync<IReadOnlyList<GatewayApiSubscriptionResponse>>(response, ct)
            ?? [];
    }

    public async Task<GatewayCancelSubscriptionResponse> CancelSubscriptionAsync(
        GatewayCancelSubscriptionRequest request,
        CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/subscriptions/cancel", request, ct);
        return await ReadSuccessAsync<GatewayCancelSubscriptionResponse>(response, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-App-Code", _settings.AppCode);
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        // Pay Gateway : sans header → Stripe Live ; X-Stripe-Env: DEV → Stripe Test.
        // Ne jamais envoyer DEV en production utilisateurs (règle Pay Gateway).
        var useSandbox = ShouldUseSandbox();
        if (useSandbox)
            request.Headers.TryAddWithoutValidation("X-Stripe-Env", "DEV");

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        _logger.LogInformation(
            "PayGateway {Method} {Path} → Stripe {StripeEnv} (UseSandbox={UseSandbox})",
            method,
            path,
            useSandbox ? "DEV/TEST" : "LIVE",
            _settings.UseSandbox);
        return await _http.SendAsync(request, ct);
    }

    /// <summary>
    /// Development / Staging → sandbox ; Production → Live.
    /// Override explicite via <see cref="PayGatewaySettings.UseSandbox"/>.
    /// </summary>
    private bool ShouldUseSandbox()
    {
        if (_settings.UseSandbox.HasValue)
            return _settings.UseSandbox.Value;

        return _environment.IsDevelopment()
            || _environment.IsStaging();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl)
            || string.IsNullOrWhiteSpace(_settings.AppCode)
            || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException(
                "La passerelle de paiement n'est pas configurée (PayGateway:BaseUrl, AppCode, ApiKey).");
        }
    }

    private static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return payload ?? throw new InvalidOperationException("Réponse vide de la passerelle de paiement.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        GatewayApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<GatewayApiError>(JsonOptions, ct);
        }
        catch
        {
            // ignored
        }

        var message = error?.Error
            ?? $"La passerelle de paiement a retourné {(int)response.StatusCode} ({response.ReasonPhrase}).";
        throw new InvalidOperationException(message);
    }
}
