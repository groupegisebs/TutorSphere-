using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

var baseUrl = "http://127.0.0.1:5099";
var key = "TutorSphere-Dev-Secret-Key-Min-32-Chars-Long!";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

var loginResp = await http.PostAsJsonAsync("api/auth/login", new { email = "admin@tutorsphere.com", password = "Admin123!" });
var loginBody = await loginResp.Content.ReadAsStringAsync();
using var loginDoc = JsonDocument.Parse(loginBody);
var token = loginDoc.RootElement.GetProperty("token").GetString()!;

var parameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = "TutorSphere",
    ValidAudience = "TutorSphere",
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
    RoleClaimType = System.Security.Claims.ClaimTypes.Role
};

try
{
    var handler = new JwtSecurityTokenHandler { MapInboundClaims = true };
    var principal = handler.ValidateToken(token, parameters, out var validated);
    Console.WriteLine($"LOCAL_VALIDATE OK name={principal.Identity?.Name} auth={principal.Identity?.IsAuthenticated}");
    foreach (var c in principal.Claims)
        Console.WriteLine($"  CLAIM {c.Type} = {c.Value}");
}
catch (Exception ex)
{
    Console.WriteLine($"LOCAL_VALIDATE FAIL {ex.GetType().Name}: {ex.Message}");
}

try
{
    var handler2 = new JwtSecurityTokenHandler { MapInboundClaims = false };
    var principal2 = handler2.ValidateToken(token, parameters, out _);
    Console.WriteLine($"LOCAL_VALIDATE_NOMAP OK auth={principal2.Identity?.IsAuthenticated} roles={string.Join(',', principal2.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))}");
}
catch (Exception ex)
{
    Console.WriteLine($"LOCAL_VALIDATE_NOMAP FAIL {ex.Message}");
}

http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var calResp = await http.GetAsync("api/calendar/view?start=2026-06-29T00:00:00Z&end=2026-08-16T00:00:00Z");
Console.WriteLine($"CAL {(int)calResp.StatusCode} {await calResp.Content.ReadAsStringAsync()}");
