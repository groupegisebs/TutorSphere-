using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TutorSphere.Web.Services;

/// <summary>
/// Typed HTTP wrapper that injects the JWT token from the current Blazor circuit's
/// AuthService on every outbound request.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        return req;
    }

    public async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Get, url));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<T?> PostAsync<T>(string url, object body) where T : class
    {
        try
        {
            var req = BuildRequest(HttpMethod.Post, url);
            req.Content = JsonContent.Create(body, options: JsonOpts);
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<T?> PutAsync<T>(string url, object body) where T : class
    {
        try
        {
            var req = BuildRequest(HttpMethod.Put, url);
            req.Content = JsonContent.Create(body, options: JsonOpts);
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<bool> DeleteAsync(string url)
    {
        try
        {
            using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Delete, url));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
