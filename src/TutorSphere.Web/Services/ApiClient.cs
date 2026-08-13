using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace TutorSphere.Web.Services;

public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null && Value is not null;
}

/// <summary>
/// Typed HTTP wrapper that injects the JWT token from the current Blazor circuit's
/// AuthService on every outbound request.
/// </summary>
public sealed class ApiClient
{
    public const string SessionExpiredMessage = "Session expirée. Veuillez vous reconnecter.";
    public const string LicenseRequiredMessage = "Licence annuelle ou auto-formation requise.";

    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly NavigationManager _nav;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http, AuthService auth, NavigationManager nav)
    {
        _http = http;
        _auth = auth;
        _nav = nav;
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url)
    {
        await _auth.EnsureSessionRestoredAsync();
        try { await _auth.RestoreActAsGroupAsync(); } catch { }
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        if (_auth.ActingAsExpertGroupId is Guid actAs)
            req.Headers.TryAddWithoutValidation("X-Act-As-Expert-Group-Id", actAs.ToString());
        return req;
    }

    private async Task<ApiResult<T>?> FailIfUnauthenticatedAsync<T>() where T : class
    {
        await _auth.EnsureSessionRestoredAsync();
        if (string.IsNullOrEmpty(_auth.Token))
        {
            if (_auth.IsSessionExpired)
                return new ApiResult<T>(null, SessionExpiredMessage);

            return new ApiResult<T>(null, "Authentification requise.");
        }

        return null;
    }

    private void HandleUnauthorizedResponse()
    {
        // Only wipe the session when the JWT itself is expired.
        // A 401 from API misconfiguration (e.g. auth scheme) must not log the user out.
        if (string.IsNullOrEmpty(_auth.Token))
            return;

        if (AuthService.IsJwtExpired(_auth.Token))
            _auth.MarkSessionExpired();
    }

    private static ApiResult<T> UnauthorizedResult<T>() where T : class =>
        new(null, SessionExpiredMessage);

    private static ApiResult<T> ForbiddenResult<T>() where T : class =>
        new(null, "Accès refusé. Vérifiez vos droits ou le mode administrateur du groupe.");

    private ApiResult<T> FailFromResponse<T>(HttpResponseMessage resp, string responseBody) where T : class
    {
        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            var detail = ExtractError(responseBody);
            return string.IsNullOrWhiteSpace(detail)
                ? ForbiddenResult<T>()
                : new ApiResult<T>(null, detail);
        }

        if (resp.StatusCode == HttpStatusCode.PaymentRequired)
        {
            TryRedirectLicenseGate(responseBody);
            return new ApiResult<T>(null, ExtractError(responseBody) ?? LicenseRequiredMessage);
        }

        var error = ExtractError(responseBody)
            ?? $"La requête a échoué ({(int)resp.StatusCode}).";
        return new ApiResult<T>(null, error);
    }

    private void TryRedirectLicenseGate(string responseBody)
    {
        try
        {
            var path = new Uri(_nav.Uri).AbsolutePath;
            if (path.Contains("/tutor/activate", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/tutor/onboarding", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/login", StringComparison.OrdinalIgnoreCase))
                return;

            string? activateUrl = null;
            string? code = null;
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("activateUrl", out var urlEl)
                    && urlEl.ValueKind == JsonValueKind.String)
                    activateUrl = urlEl.GetString();
                if (doc.RootElement.TryGetProperty("code", out var codeEl)
                    && codeEl.ValueKind == JsonValueKind.String)
                    code = codeEl.GetString();
            }

            var target = !string.IsNullOrWhiteSpace(activateUrl)
                ? activateUrl!
                : string.Equals(code, "ONBOARDING_REQUIRED", StringComparison.OrdinalIgnoreCase)
                    ? "/tutor/onboarding"
                    : "/tutor/activate";

            _nav.NavigateTo(target, forceLoad: true);
        }
        catch
        {
            // Ne pas casser l'appel API si la navigation échoue.
        }
    }

    public async Task<ApiResult<T>> GetWithErrorAsync<T>(string url) where T : class
    {
        var authFailure = await FailIfUnauthenticatedAsync<T>();
        if (authFailure is not null)
            return authFailure;

        try
        {
            using var resp = await _http.SendAsync(await BuildRequestAsync(HttpMethod.Get, url));
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return UnauthorizedResult<T>();
            }

            if (!resp.IsSuccessStatusCode)
                return FailFromResponse<T>(resp, responseBody);

            if (string.IsNullOrWhiteSpace(responseBody))
                return new ApiResult<T>(null, "Réponse vide du serveur.");

            var value = JsonSerializer.Deserialize<T>(responseBody, JsonOpts);
            if (value is null)
                return new ApiResult<T>(null, "Réponse inattendue du serveur.");

            return new ApiResult<T>(value, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<T>(null, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    public async Task<T?> GetAsync<T>(string url) where T : class
    {
        var result = await GetWithErrorAsync<T>(url);
        return result.Value;
    }

    public async Task<T?> PostAsync<T>(string url, object body) where T : class
    {
        var result = await PostWithErrorAsync<T>(url, body);
        return result.Value;
    }

    public async Task<ApiResult<T>> PostWithErrorAsync<T>(string url, object body) where T : class
    {
        var authFailure = await FailIfUnauthenticatedAsync<T>();
        if (authFailure is not null)
            return authFailure;

        try
        {
            var req = await BuildRequestAsync(HttpMethod.Post, url);
            req.Content = JsonContent.Create(body, options: JsonOpts);
            using var resp = await _http.SendAsync(req);
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return UnauthorizedResult<T>();
            }

            if (!resp.IsSuccessStatusCode)
                return FailFromResponse<T>(resp, responseBody);

            // 204 / empty body is a valid success for some actions (cancel, etc.).
            if (string.IsNullOrWhiteSpace(responseBody) || resp.StatusCode == HttpStatusCode.NoContent)
                return new ApiResult<T>(null, null);

            var value = JsonSerializer.Deserialize<T>(responseBody, JsonOpts);
            if (value is null)
                return new ApiResult<T>(null, "Réponse inattendue du serveur.");

            return new ApiResult<T>(value, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<T>(null, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    public async Task<ApiResult<T>> PutWithErrorAsync<T>(string url, object body) where T : class
    {
        var authFailure = await FailIfUnauthenticatedAsync<T>();
        if (authFailure is not null)
            return authFailure;

        try
        {
            var req = await BuildRequestAsync(HttpMethod.Put, url);
            req.Content = JsonContent.Create(body, options: JsonOpts);
            using var resp = await _http.SendAsync(req);
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return UnauthorizedResult<T>();
            }

            if (!resp.IsSuccessStatusCode)
                return FailFromResponse<T>(resp, responseBody);

            if (string.IsNullOrWhiteSpace(responseBody))
                return new ApiResult<T>(null, "Réponse vide du serveur.");

            var value = JsonSerializer.Deserialize<T>(responseBody, JsonOpts);
            if (value is null)
                return new ApiResult<T>(null, "Réponse inattendue du serveur.");

            return new ApiResult<T>(value, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<T>(null, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    public async Task<T?> PutAsync<T>(string url, object body) where T : class
    {
        var result = await PutWithErrorAsync<T>(url, body);
        return result.Value;
    }

    public async Task<ApiResult<bool>> DeleteWithErrorAsync(string url)
    {
        await _auth.EnsureSessionRestoredAsync();
        if (string.IsNullOrEmpty(_auth.Token))
        {
            if (_auth.IsSessionExpired)
                return new ApiResult<bool>(false, SessionExpiredMessage);

            return new ApiResult<bool>(false, "Authentification requise.");
        }

        try
        {
            using var resp = await _http.SendAsync(await BuildRequestAsync(HttpMethod.Delete, url));
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return new ApiResult<bool>(false, SessionExpiredMessage);
            }

            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                var detail = ExtractError(responseBody);
                return new ApiResult<bool>(false,
                    string.IsNullOrWhiteSpace(detail)
                        ? "Accès refusé. Vérifiez vos droits ou le mode administrateur du groupe."
                        : detail);
            }

            if (resp.StatusCode == HttpStatusCode.PaymentRequired)
            {
                TryRedirectLicenseGate(responseBody);
                return new ApiResult<bool>(false, ExtractError(responseBody) ?? LicenseRequiredMessage);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var error = ExtractError(responseBody)
                    ?? $"La requête a échoué ({(int)resp.StatusCode}).";
                return new ApiResult<bool>(false, error);
            }

            return new ApiResult<bool>(true, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<bool>(false, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    public async Task<bool> DeleteAsync(string url)
    {
        var result = await DeleteWithErrorAsync(url);
        return result.Error is null;
    }

    public async Task<ApiResult<byte[]>> GetBytesWithErrorAsync(string url)
    {
        var authFailure = await FailIfUnauthenticatedAsync<byte[]>();
        if (authFailure is not null)
            return new ApiResult<byte[]>(null, authFailure.Error);

        try
        {
            var req = await BuildRequestAsync(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return UnauthorizedResult<byte[]>();
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return FailFromResponse<byte[]>(resp, body);
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return new ApiResult<byte[]>(bytes, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<byte[]>(null, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    public async Task<ApiResult<T>> PostMultipartAsync<T>(string url, MultipartFormDataContent content) where T : class
    {
        var authFailure = await FailIfUnauthenticatedAsync<T>();
        if (authFailure is not null)
            return authFailure;

        try
        {
            var req = await BuildRequestAsync(HttpMethod.Post, url);
            req.Content = content;
            using var resp = await _http.SendAsync(req);
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorizedResponse();
                return UnauthorizedResult<T>();
            }

            if (!resp.IsSuccessStatusCode)
                return FailFromResponse<T>(resp, responseBody);

            if (string.IsNullOrWhiteSpace(responseBody))
                return new ApiResult<T>(null, "Réponse vide du serveur.");

            var value = JsonSerializer.Deserialize<T>(responseBody, JsonOpts);
            if (value is null)
                return new ApiResult<T>(null, "Réponse inattendue du serveur.");

            return new ApiResult<T>(value, null);
        }
        catch (Exception ex)
        {
            return new ApiResult<T>(null, $"Erreur de connexion à l'API : {ex.Message}");
        }
    }

    internal static string? ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                return e.GetString();

            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();

            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()))
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
                if (messages.Count > 0)
                    return string.Join(" ", messages!);
            }
        }
        catch (JsonException) { return body.Trim(); }

        return null;
    }
}
