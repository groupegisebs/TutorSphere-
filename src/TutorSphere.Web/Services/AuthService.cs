using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace TutorSphere.Web.Services;

/// <summary>
/// Holds login state for the current Blazor circuit.
/// Tokens are mirrored to sessionStorage so interactive circuits can restore auth after reconnect.
/// </summary>
public sealed class AuthService
{
    private readonly HttpClient _http;
    private readonly CustomAuthenticationStateProvider _authProvider;
    private readonly IJSRuntime _js;
    private bool _restoreAttempted;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AuthService(HttpClient http, AuthenticationStateProvider authProvider, IJSRuntime js)
    {
        _http = http;
        _authProvider = (CustomAuthenticationStateProvider)authProvider;
        _js = js;
    }

    public bool IsAuthenticated => _authProvider.IsAuthenticated;
    public string? UserEmail => _authProvider.UserEmail;
    public string? UserName => _authProvider.UserName;
    public string? PrimaryRole => _authProvider.PrimaryRole;
    public string? Token => _authProvider.Token;
    public Guid? TenantId => _authProvider.TenantId;

    /// <summary>Calls the API login endpoint and caches the result.</summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login",
                new { email, password });

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var msg = TryExtractError(body) ?? "Identifiants invalides.";
                return new LoginResult(false, msg, null);
            }

            var result = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                return new LoginResult(false, "Réponse inattendue du serveur.", null);

            _authProvider.MarkAuthenticated(result);
            await PersistSessionAsync(result);
            _restoreAttempted = true;

            var role = result.Role ?? "";
            return new LoginResult(true, null, RoleToRoute(role));
        }
        catch (Exception ex)
        {
            return new LoginResult(false, $"Erreur de connexion à l'API : {ex.Message}", null);
        }
    }

    public void Logout()
    {
        _authProvider.MarkLoggedOut();
        _restoreAttempted = false;
        _ = ClearSessionAsync();
    }

    /// <summary>Restores auth from sessionStorage when the circuit lost in-memory state.</summary>
    public async Task EnsureSessionRestoredAsync()
    {
        if (!string.IsNullOrEmpty(Token))
            return;

        if (_restoreAttempted)
            return;

        _restoreAttempted = true;

        try
        {
            var json = await _js.InvokeAsync<string?>("tsAuth.load");
            if (string.IsNullOrWhiteSpace(json))
                return;

            var auth = JsonSerializer.Deserialize<AuthResponse>(json, JsonOpts);
            if (auth is not null && !string.IsNullOrWhiteSpace(auth.Token))
                _authProvider.MarkAuthenticated(auth);
        }
        catch (InvalidOperationException)
        {
            // Static prerender — JS interop unavailable; allow retry later
            _restoreAttempted = false;
        }
        catch (JSException) { }
    }

    private async Task PersistSessionAsync(AuthResponse auth)
    {
        try
        {
            var json = JsonSerializer.Serialize(auth, JsonOpts);
            await _js.InvokeVoidAsync("tsAuth.save", json);
        }
        catch (InvalidOperationException) { }
        catch (JSException) { }
    }

    private async Task ClearSessionAsync()
    {
        try { await _js.InvokeVoidAsync("tsAuth.clear"); }
        catch (InvalidOperationException) { }
        catch (JSException) { }
    }

    private static string RoleToRoute(string role) => role switch
    {
        "SuperAdmin" or "PlatformAdmin" => "/admin/dashboard",
        "Tutor" or "TeachingAssistant" => "/tutor/dashboard",
        "Parent" => "/parent",
        "Student" => "/student/dashboard",
        _ => "/tutor/dashboard"
    };

    private static string? TryExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString();
        }
        catch (JsonException) { return body; }
        return body;
    }
}

public sealed record LoginResult(bool Success, string? Error, string? RedirectTo);

internal sealed record AuthResponse(
    string Token,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    DateTime ExpiresAt);

/// <summary>
/// Circuit-scoped authentication state.
/// The state lives as long as the Blazor Server circuit; sessionStorage restores it after reconnect.
/// </summary>
public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _user = new(new ClaimsIdentity());

    public bool IsAuthenticated => _user.Identity?.IsAuthenticated == true;
    public string? UserEmail => _user.FindFirst(ClaimTypes.Email)?.Value;
    public string? UserName => _user.FindFirst(ClaimTypes.Name)?.Value;
    public string? PrimaryRole => _user.FindFirst(ClaimTypes.Role)?.Value;

    public Guid? TenantId
    {
        get
        {
            var val = _user.FindFirst("tenant_id")?.Value;
            return val is not null && Guid.TryParse(val, out var g) ? g : null;
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_user));

    internal string? Token { get; private set; }

    internal void MarkAuthenticated(AuthResponse auth)
    {
        Token = auth.Token;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(auth.FullName) ? auth.Email : auth.FullName)
        };

        if (!string.IsNullOrWhiteSpace(auth.Role))
            claims.Add(new Claim(ClaimTypes.Role, auth.Role));

        if (auth.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", auth.TenantId.Value.ToString()));

        // Fallback: parse JWT payload for role/tenant if missing from response body
        foreach (var (key, value) in ParseJwtPayloadClaims(auth.Token))
        {
            if (key is "tenant_id" && claims.All(c => c.Type != "tenant_id"))
                claims.Add(new Claim("tenant_id", value));
            if (key is "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                && claims.All(c => c.Type != ClaimTypes.Role))
                claims.Add(new Claim(ClaimTypes.Role, value));
        }

        _user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TutorSphereJwt"));
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static IEnumerable<(string Key, string Value)> ParseJwtPayloadClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) yield break;
        var payload = parts[1];
        var mod = payload.Length % 4;
        if (mod != 0) payload += new string('=', 4 - mod);
        string json;
        try { json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'))); }
        catch { yield break; }
        List<(string, string)> pairs = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                    pairs.Add((prop.Name, prop.Value.GetString()!));
        }
        catch { }
        foreach (var pair in pairs)
            yield return pair;
    }

    internal void MarkLoggedOut()
    {
        Token = null;
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
