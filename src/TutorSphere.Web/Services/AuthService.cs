using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace TutorSphere.Web.Services;

/// <summary>
/// Holds login state for the current Blazor circuit.
/// JWT is mirrored to an HttpOnly cookie (BFF) and sessionStorage for circuit restore.
/// </summary>
public sealed class AuthService
{
    private readonly HttpClient _http;
    private readonly CustomAuthenticationStateProvider _authProvider;
    private readonly IJSRuntime _js;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService> _logger;
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private bool _jsRestoreAttempted;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AuthService(
        HttpClient http,
        AuthenticationStateProvider authProvider,
        IJSRuntime js,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger)
    {
        _http = http;
        _authProvider = (CustomAuthenticationStateProvider)authProvider;
        _js = js;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public bool IsAuthenticated => _authProvider.IsAuthenticated;
    public bool IsSessionExpired { get; private set; }

    /// <summary>True once cookie and/or sessionStorage restore has been attempted.</summary>
    public bool SessionRestoreCompleted { get; private set; }

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
                var apiError = TryExtractError(body);
                var msg = apiError is not null
                    ? $"({(int)resp.StatusCode}) {apiError}"
                    : $"({(int)resp.StatusCode}) Identifiants invalides.";
                return new LoginResult(false, msg, null);
            }

            var result = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                return new LoginResult(false, "Réponse inattendue du serveur.", null);

            ApplyAuthenticatedSession(result);
            await PersistSessionAsync(result);

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
        IsSessionExpired = false;
        SessionRestoreCompleted = false;
        _jsRestoreAttempted = false;
        _authProvider.MarkLoggedOut();
        ClearAuthCookie();
        _ = ClearSessionAsync();
    }

    /// <summary>Marks the session as expired without forcing a navigation loop.</summary>
    public void MarkSessionExpired()
    {
        if (IsSessionExpired && string.IsNullOrEmpty(Token))
            return;

        _logger.LogInformation("Auth session marked expired.");
        IsSessionExpired = true;
        _authProvider.MarkLoggedOut();
        SessionRestoreCompleted = false;
        _jsRestoreAttempted = false;
        ClearAuthCookie();
        _ = ClearSessionAsync();
    }

    /// <summary>
    /// Restores auth from HttpOnly cookie and/or sessionStorage when the circuit lost in-memory state.
    /// </summary>
    /// <param name="forceJs">When true, retries sessionStorage even if a prior prerender attempt failed.</param>
    public async Task EnsureSessionRestoredAsync(bool forceJs = false)
    {
        if (!string.IsNullOrEmpty(Token))
        {
            SessionRestoreCompleted = true;
            return;
        }

        await _restoreLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(Token))
            {
                SessionRestoreCompleted = true;
                return;
            }

            if (TryRestoreFromCookie())
            {
                SessionRestoreCompleted = true;
                _logger.LogDebug("Auth restored from HttpOnly cookie.");
                return;
            }

            if (_jsRestoreAttempted && !forceJs)
            {
                SessionRestoreCompleted = true;
                return;
            }

            try
            {
                var json = await _js.InvokeAsync<string?>("tsAuth.load");
                _jsRestoreAttempted = true;
                SessionRestoreCompleted = true;

                if (string.IsNullOrWhiteSpace(json))
                    return;

                var auth = JsonSerializer.Deserialize<AuthResponse>(json, JsonOpts);
                if (auth is null || string.IsNullOrWhiteSpace(auth.Token))
                    return;

                if (IsStoredSessionExpired(auth))
                {
                    _logger.LogInformation("Stored session expired during JS restore.");
                    MarkSessionExpired();
                    return;
                }

                ApplyAuthenticatedSession(auth);
                _logger.LogDebug("Auth restored from sessionStorage.");
            }
            catch (InvalidOperationException)
            {
                // Static prerender — JS interop unavailable; retry after first interactive render
            }
            catch (JSException ex)
            {
                _jsRestoreAttempted = true;
                SessionRestoreCompleted = true;
                _logger.LogDebug(ex, "sessionStorage restore failed.");
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    internal static CookieOptions BuildCookieOptions(DateTime expiresAtUtc, bool secure)
    {
        var maxAge = expiresAtUtc.ToUniversalTime() - DateTime.UtcNow;
        if (maxAge < TimeSpan.Zero)
            maxAge = TimeSpan.FromHours(1);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = maxAge
        };
    }

    internal void SetAuthCookie(string token, DateTime expiresAtUtc)
    {
        var ctx = _httpContextAccessor.HttpContext;
        // Blazor Server interactive circuit: response already started — cookie must go via /bff/auth/establish (JS).
        if (ctx is null || ctx.Response.HasStarted || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            ctx.Response.Cookies.Append(
                AuthCookieConstants.CookieName,
                token,
                BuildCookieOptions(expiresAtUtc, ctx.Request.IsHttps));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Skipping auth cookie set; response already started.");
        }
    }

    internal void ClearAuthCookie()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null || ctx.Response.HasStarted)
            return;

        try
        {
            ctx.Response.Cookies.Delete(AuthCookieConstants.CookieName, new CookieOptions { Path = "/" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Skipping auth cookie clear; response already started.");
        }
    }

    private void ApplyAuthenticatedSession(AuthResponse auth)
    {
        IsSessionExpired = false;
        _authProvider.MarkAuthenticated(auth);
    }

    private bool TryRestoreFromCookie()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Cookies[AuthCookieConstants.CookieName];
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (IsJwtExpired(token))
        {
            _logger.LogInformation("HttpOnly auth cookie JWT expired.");
            ClearAuthCookie();
            return false;
        }

        var auth = AuthResponseFromJwt(token);
        if (auth is null)
            return false;

        ApplyAuthenticatedSession(auth);
        return true;
    }

    private async Task PersistSessionAsync(AuthResponse auth)
    {
        // Prefer BFF cookie via JS during interactive circuits; fall back to direct cookie on HTTP requests.
        SetAuthCookie(auth.Token, auth.ExpiresAt);

        try
        {
            var json = JsonSerializer.Serialize(auth, JsonOpts);
            await _js.InvokeVoidAsync("tsAuth.persist", json);
        }
        catch (InvalidOperationException)
        {
            // Prerender / no JS — cookie already set above when response allows it.
        }
        catch (JSException ex)
        {
            _logger.LogDebug(ex, "Could not persist auth to sessionStorage/cookie via JS.");
        }
    }

    private async Task ClearSessionAsync()
    {
        try { await _js.InvokeVoidAsync("tsAuth.clearAll"); }
        catch (InvalidOperationException) { }
        catch (JSException) { }
    }

    internal static bool IsStoredSessionExpired(AuthResponse auth)
    {
        if (auth.ExpiresAt != default && auth.ExpiresAt.ToUniversalTime() <= DateTime.UtcNow)
            return true;

        return IsJwtExpired(auth.Token);
    }

    internal static bool IsJwtExpired(string token)
    {
        var exp = TryGetJwtExpiry(token);
        return exp is not null && exp <= DateTimeOffset.UtcNow;
    }

    internal static AuthResponse? AuthResponseFromJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var payload = parts[1];
        var mod = payload.Length % 4;
        if (mod != 0)
            payload += new string('=', 4 - mod);

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));
            using var doc = JsonDocument.Parse(json);

            var email = GetClaim(doc, ClaimTypes.Email, "email") ?? "";
            var name = GetClaim(doc, ClaimTypes.Name, "name", "unique_name") ?? email;
            var role = GetClaim(doc, ClaimTypes.Role, "role") ?? "";
            var tenantRaw = GetClaim(doc, "tenant_id");
            Guid? tenantId = tenantRaw is not null && Guid.TryParse(tenantRaw, out var g) ? g : null;

            var expiresAt = TryGetJwtExpiry(token)?.UtcDateTime ?? DateTime.UtcNow.AddHours(24);
            return new AuthResponse(token, email, name, role, tenantId, expiresAt);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetClaim(JsonDocument doc, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }

        return null;
    }

    private static DateTimeOffset? TryGetJwtExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var payload = parts[1];
        var mod = payload.Length % 4;
        if (mod != 0)
            payload += new string('=', 4 - mod);

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number)
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
        }
        catch
        {
            return null;
        }
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
