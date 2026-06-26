using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Auth;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Identity;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _email;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IEmailService email)
    {
        _userManager = userManager;
        _configuration = configuration;
        _email = email;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var role = NormalizeRole(request.Role);
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, role);

        await _email.SendWelcomeAsync(user.Email!, user.FirstName, ct);

        return await BuildAuthResponse(user, role);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? UserRoles.Parent;
        return await BuildAuthResponse(user, role);
    }

    private async Task<AuthResponse> BuildAuthResponse(ApplicationUser user, string role)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var expires = DateTime.UtcNow.AddHours(double.Parse(jwtSection["ExpireHours"] ?? "24"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role)
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResponse(tokenString, user.Email ?? string.Empty, user.FullName, role, user.TenantId, expires);
    }

    private static string NormalizeRole(string role) =>
        UserRoles.All.FirstOrDefault(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
        ?? UserRoles.Parent;
}
