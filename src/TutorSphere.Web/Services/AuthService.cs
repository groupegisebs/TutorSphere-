using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace TutorSphere.Web.Services;

/// <summary>
/// Holds login state for the current Blazor circuit.
/// On a page reload the state is lost and the user lands on /login again.
/// A future phase can promote tokens to HttpOnly cookies for persistence.
/// </summary>
public sealed class AuthService
{
    private readonly HttpClient _http;
    private readonly CustomAuthenticationStateProvider _authProvider;

    public AuthService(HttpClient http, AuthenticationStateProvider authProvider)
    {
        _http = http;
        _authProvider = (CustomAuthenticationStateProvider)authProvider;
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

            var result = await resp.Content.ReadFromJsonAsync<AuthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (result is null) return new LoginResult(false, "Réponse inattendue du serveur.", null);

            _authProvider.MarkAuthenticated(result);
            var role = result.Roles?.FirstOrDefault() ?? "";
            return new LoginResult(true, null, RoleToRoute(role));
        }
        catch (Exception ex)
        {
            return new LoginResult(false, $"Erreur de connexion à l'API : {ex.Message}", null);
        }
    }

    public void Logout() => _authProvider.MarkLoggedOut();

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
    string? FirstName,
    string? LastName,
    IReadOnlyList<string>? Roles);

/// <summary>
/// Circuit-scoped authentication state.
/// The state lives as long as the Blazor Server circuit; it does not persist across browser refreshes.
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
            new(ClaimTypes.Name,
                !string.IsNullOrWhiteSpace(auth.FirstName)
                    ? $"{auth.FirstName} {auth.LastName}".Trim()
                    : auth.Email)
        };

        if (auth.Roles is not null)
            claims.AddRange(auth.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Parse additional claims from JWT payload (e.g. tenant_id)
        foreach (var (key, value) in ParseJwtPayloadClaims(auth.Token))
        {
            if (key is "tenant_id" or "sub")
                claims.Add(new Claim(key, value));
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
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                    yield return (prop.Name, prop.Value.GetString()!);
        }
        catch { }
    }

    internal void MarkLoggedOut()
    {
        Token = null;
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
