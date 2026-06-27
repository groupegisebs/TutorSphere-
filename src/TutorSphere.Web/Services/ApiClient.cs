using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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

    private readonly HttpClient _http;
    private readonly AuthService _auth;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url)
    {
        await _auth.EnsureSessionRestoredAsync();
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        return req;
    }

    private async Task<ApiResult<T>?> FailIfUnauthenticatedAsync<T>() where T : class
    {
        await _auth.EnsureSessionRestoredAsync();
        if (string.IsNullOrEmpty(_auth.Token))
            return new ApiResult<T>(null, SessionExpiredMessage);
        return null;
    }

    private static ApiResult<T> UnauthorizedResult<T>() where T : class =>
        new(null, SessionExpiredMessage);

    public async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            if (string.IsNullOrEmpty(_auth.Token))
            {
                await _auth.EnsureSessionRestoredAsync();
                if (string.IsNullOrEmpty(_auth.Token))
                    return null;
            }

            using var resp = await _http.SendAsync(await BuildRequestAsync(HttpMethod.Get, url));
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.Logout();
                return null;
            }

            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        }
        catch { return null; }
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
                _auth.Logout();
                return UnauthorizedResult<T>();
            }

            if (!resp.IsSuccessStatusCode)
            {
                var error = ExtractError(responseBody)
                    ?? $"La requête a échoué ({(int)resp.StatusCode}).";
                return new ApiResult<T>(null, error);
            }

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
        try
        {
            if (string.IsNullOrEmpty(_auth.Token))
            {
                await _auth.EnsureSessionRestoredAsync();
                if (string.IsNullOrEmpty(_auth.Token))
                    return null;
            }

            var req = await BuildRequestAsync(HttpMethod.Put, url);
            req.Content = JsonContent.Create(body, options: JsonOpts);
            using var resp = await _http.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.Logout();
                return null;
            }

            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<bool> DeleteAsync(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(_auth.Token))
            {
                await _auth.EnsureSessionRestoredAsync();
                if (string.IsNullOrEmpty(_auth.Token))
                    return false;
            }

            using var resp = await _http.SendAsync(await BuildRequestAsync(HttpMethod.Delete, url));
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.Logout();
                return false;
            }

            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
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
