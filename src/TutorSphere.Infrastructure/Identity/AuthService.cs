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
    Task<AuthResponse> LoginChildAsync(ChildLoginRequest request, CancellationToken ct = default);
    Task<ChildLoginAccessDto> EnableChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task<ChildLoginAccessDto> RegenerateChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task RevokeChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task<RegisterSchoolResponse> RegisterSchoolAsync(RegisterSchoolRequest request, CancellationToken ct = default);
    Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default);
    Task EnsureParentProfileForUserAsync(string userId, CancellationToken ct = default);
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

        if (UserRoles.ParentPortalRoles.Contains(role))
            await EnsureParentProfileAsync(user, ct);

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

        if (UserRoles.ParentPortalRoles.Contains(role))
            await EnsureParentProfileAsync(user, ct);

        return await BuildAuthResponse(user, role);
    }

    public async Task<AuthResponse> LoginChildAsync(ChildLoginRequest request, CancellationToken ct = default)
    {
        var parentEmail = request.ParentEmail.Trim();
        var accessCode = request.AccessCode.Trim();
        if (string.IsNullOrWhiteSpace(parentEmail) || string.IsNullOrWhiteSpace(accessCode))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var parentUser = await _userManager.FindByEmailAsync(parentEmail)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUser.Id)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        var codeNorm = accessCode.Trim();
        var student = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id && s.IsActive && s.LoginAccessCode != null)
            .AsEnumerable()
            .FirstOrDefault(s =>
                string.Equals(s.LoginAccessCode, codeNorm, StringComparison.Ordinal));

        if (student is null || string.IsNullOrEmpty(student.UserId))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var user = await _userManager.FindByIdAsync(student.UserId)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Ce compte est désactivé. Contactez votre parent.");

        if (!await _userManager.CheckPasswordAsync(user, codeNorm))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(r => r == UserRoles.Student) ?? UserRoles.Student;
        return await BuildAuthResponse(user, role);
    }

    public Task<ChildLoginAccessDto> EnableChildLoginAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct = default) =>
        ProvisionOrRegenerateChildAccessAsync(parentUserId, studentId, ct);

    public Task<ChildLoginAccessDto> RegenerateChildLoginAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct = default) =>
        ProvisionOrRegenerateChildAccessAsync(parentUserId, studentId, ct);

    public async Task RevokeChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default)
    {
        var student = await GetOwnedChildAsync(parentUserId, studentId, ct);

        if (!string.IsNullOrEmpty(student.UserId))
        {
            var user = await _userManager.FindByIdAsync(student.UserId);
            if (user is not null)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
        }

        student.UserId = null;
        student.LoginAccessCode = null;
        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ChildLoginAccessDto> ProvisionOrRegenerateChildAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct)
    {
        var student = await GetOwnedChildAsync(parentUserId, studentId, ct);
        var accessCode = GenerateChildAccessCode();

        ApplicationUser user;
        if (!string.IsNullOrEmpty(student.UserId))
        {
            user = await _userManager.FindByIdAsync(student.UserId)
                ?? throw new InvalidOperationException("Compte de connexion introuvable pour cet enfant.");

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.SetLockoutEnabledAsync(user, false);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, accessCode);
            if (!reset.Succeeded)
                throw new InvalidOperationException(string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
        else
        {
            var loginEmail = await ResolveChildIdentityEmailAsync(student, ct);
            user = new ApplicationUser
            {
                UserName = loginEmail,
                Email = loginEmail,
                FirstName = student.FirstName,
                LastName = student.LastName,
                EmailConfirmed = true,
                TenantId = student.TenantId
            };

            var create = await _userManager.CreateAsync(user, accessCode);
            if (!create.Succeeded)
                throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, UserRoles.Student);
            student.UserId = user.Id;
        }

        student.LoginAccessCode = accessCode;
        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var parent = _db.ParentProfilesForAnyTenant.First(p => p.UserId == parentUserId);
        var hint = string.IsNullOrWhiteSpace(student.Email)
            ? $"Connexion : e-mail du parent ({parent.Email}) + ce code"
            : $"Connexion : e-mail du parent ({parent.Email}) + ce code, ou le courriel de l'enfant avec le code";

        return new ChildLoginAccessDto(student.Id, true, accessCode, hint);
    }

    private async Task<Student> GetOwnedChildAsync(string parentUserId, Guid studentId, CancellationToken ct)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == studentId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        await Task.CompletedTask;
        return student;
    }

    private async Task<string> ResolveChildIdentityEmailAsync(Student student, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(student.Email))
        {
            var email = student.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is null)
                return email;
        }

        // E-mail synthétique unique : un parent peut avoir plusieurs enfants sans adresse.
        return $"child.{student.Id:N}@child.tutorsphere.local";
    }

    /// <summary>Code 8 caractères respectant la politique Identity (digit + length 8 + maj/min/symbole).</summary>
    private static string GenerateChildAccessCode()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        Span<char> code = stackalloc char[8];
        code[0] = upper[Random.Shared.Next(upper.Length)];
        code[1] = upper[Random.Shared.Next(upper.Length)];
        code[2] = digits[Random.Shared.Next(digits.Length)];
        code[3] = digits[Random.Shared.Next(digits.Length)];
        code[4] = digits[Random.Shared.Next(digits.Length)];
        code[5] = digits[Random.Shared.Next(digits.Length)];
        code[6] = lower[Random.Shared.Next(lower.Length)];
        code[7] = '!';
        return new string(code);
    }

    public async Task EnsureParentProfileForUserAsync(string userId, CancellationToken ct = default)
    {
        if (_db.ParentProfilesForAnyTenant.Any(p => p.UserId == userId))
            return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return;

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(r => UserRoles.ParentPortalRoles.Contains(r)))
            return;

        await EnsureParentProfileAsync(user, ct);
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
            OwnerUserId = user.Id,
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

        string? tenantName = null;
        if (user.TenantId.HasValue)
        {
            tenantName = _db.Tenants
                .Where(t => t.Id == user.TenantId.Value)
                .Select(t => t.Name)
                .FirstOrDefault();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role)
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(tenantName))
            claims.Add(new Claim("tenant_name", tenantName));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResponse(tokenString, user.Email ?? string.Empty, user.FullName, role, user.TenantId, expires, tenantName);
    }

    private static string NormalizeRole(string role) =>
        UserRoles.All.FirstOrDefault(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
        ?? UserRoles.Parent;

    private async Task EnsureParentProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        if (_db.ParentProfilesForAnyTenant.Any(p => p.UserId == user.Id))
            return;

        var tenantId = user.TenantId ?? _db.Tenants.Select(t => t.Id).FirstOrDefault();
        if (tenantId == Guid.Empty)
            return;

        _db.Add(new ParentProfile
        {
            TenantId = tenantId,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? user.UserName ?? string.Empty
        });
        await _db.SaveChangesAsync(ct);
    }
}
