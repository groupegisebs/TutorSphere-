using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Auth;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Identity;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<RegisterSchoolResponse> RegisterSchoolAsync(RegisterSchoolRequest request, CancellationToken ct = default);
    Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _email;
    private readonly IApplicationDbContext _db;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IEmailService email,
        IApplicationDbContext db)
    {
        _userManager = userManager;
        _configuration = configuration;
        _email = email;
        _db = db;
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

        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var apiBase = (_configuration["ApiBaseUrl"] ?? "https://api.tutorsphere.gisebs.com").TrimEnd('/');
        var confirmUrl = $"{apiBase}/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(confirmToken)}";
        await _email.SendEmailConfirmationSimpleAsync(user.Email!, user.FirstName, confirmUrl, ct);

        return await BuildAuthResponse(user, role);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Ce compte est désactivé. Contactez l'administrateur.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? UserRoles.Parent;
        return await BuildAuthResponse(user, role);
    }

    public async Task<RegisterSchoolResponse> RegisterSchoolAsync(RegisterSchoolRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (_db.Tenants.Any(t => t.Slug == slug))
            throw new InvalidOperationException("Cette adresse est déjà utilisée par une autre école.");

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

        await _userManager.AddToRoleAsync(user, UserRoles.Tutor);

        var tenant = new Tenant
        {
            Name = request.SchoolName.Trim(),
            Slug = slug,
            Subdomain = slug,
            City = request.City,
            Country = request.Country ?? "CA",
            Status = TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            Branding = new TenantBranding()
        };

        _db.Add(tenant);
        await _db.SaveChangesAsync(ct);

        user.TenantId = tenant.Id;
        await _userManager.UpdateAsync(user);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var apiBase = (_configuration["ApiBaseUrl"] ?? "https://api.tutorsphere.gisebs.com").TrimEnd('/');
        var confirmUrl = $"{apiBase}/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
        await _email.SendEmailConfirmationAsync(user.Email!, user.FirstName, confirmUrl, ct);

        return new RegisterSchoolResponse(tenant.Id, tenant.Slug, user.Email!);
    }

    public async Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            throw new InvalidOperationException("Le lien de confirmation est invalide ou expiré.");
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return; // silent — don't reveal whether email exists

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var webBase = (_configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
        var resetUrl = $"{webBase}/reset-password?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
        await _email.SendResetPasswordAsync(user.Email!, user.FirstName, resetUrl, ct);
    }

    public async Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _email.SendPasswordChangedAsync(user.Email!, user.FirstName, ct);
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
