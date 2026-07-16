using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Email;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin,PlatformAdmin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly MailGatewaySettings _mailSettings;
    private readonly MailGatewayClient _mailClient;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IApplicationDbContext db,
        IConfiguration configuration,
        IOptions<MailGatewaySettings> mailSettings,
        MailGatewayClient mailClient)
    {
        _userManager = userManager;
        _email = email;
        _db = db;
        _configuration = configuration;
        _mailSettings = mailSettings.Value;
        _mailClient = mailClient;
    }

    /// <summary>Returns users belonging to a given role.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? role = null, [FromQuery] string? q = null)
    {
        List<ApplicationUser> users;
        string resolvedRole;

        if (string.IsNullOrWhiteSpace(role) || role.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            resolvedRole = "All";
            var tutors = await _userManager.GetUsersInRoleAsync(UserRoles.Tutor);
            var parents = await _userManager.GetUsersInRoleAsync(UserRoles.Parent);
            var students = await _userManager.GetUsersInRoleAsync(UserRoles.Student);
            var tas = await _userManager.GetUsersInRoleAsync(UserRoles.TeachingAssistant);
            users = tutors.Concat(parents).Concat(students).Concat(tas)
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();
        }
        else
        {
            if (!UserRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { error = "Rôle inconnu." });
            resolvedRole = role;
            users = (await _userManager.GetUsersInRoleAsync(role)).ToList();
        }

        var tenantIds = users.Where(u => u.TenantId.HasValue).Select(u => u.TenantId!.Value).Distinct().ToList();
        var tenants = _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Country, t.City })
            .ToDictionary(t => t.Id);

        var roleCache = new Dictionary<string, string>();
        async Task<string> ResolveRoleAsync(ApplicationUser u)
        {
            if (resolvedRole != "All") return resolvedRole;
            if (roleCache.TryGetValue(u.Id, out var cached)) return cached;
            var roles = await _userManager.GetRolesAsync(u);
            var primary = roles.FirstOrDefault(r => r is not (UserRoles.SuperAdmin or UserRoles.PlatformAdmin))
                          ?? roles.FirstOrDefault()
                          ?? "User";
            roleCache[u.Id] = primary;
            return primary;
        }

        var result = new List<AdminUserDto>();
        foreach (var u in users)
        {
            var userRole = await ResolveRoleAsync(u);
            tenants.TryGetValue(u.TenantId ?? Guid.Empty, out var tenant);
            result.Add(new AdminUserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FullName,
                userRole,
                u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                u.PhoneNumber,
                tenant?.Country,
                tenant?.City,
                tenant?.Name,
                u.TenantId,
                null,
                null));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            result = result.Where(u =>
                u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (u.Phone != null && u.Phone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (u.SchoolName != null && u.SchoolName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return Ok(result.OrderBy(u => u.FullName).ToList());
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(r => r is not (UserRoles.SuperAdmin or UserRoles.PlatformAdmin))
                   ?? roles.FirstOrDefault()
                   ?? "User";

        string? schoolName = null;
        string? country = null;
        string? city = null;
        if (user.TenantId is Guid tid)
        {
            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid);
            schoolName = tenant?.Name;
            country = tenant?.Country;
            city = tenant?.City;
        }

        return Ok(new AdminUserDetailDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.FirstName,
            user.LastName,
            role,
            user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow,
            user.PhoneNumber,
            country,
            city,
            schoolName,
            user.TenantId,
            user.PreferredLanguage,
            user.TimeZone,
            null,
            null));
    }

    /// <summary>Unlocks a user account.</summary>
    [HttpPost("users/{userId}/activate")]
    public async Task<IActionResult> ActivateUser(string userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        user.LockoutEnd = null;
        user.LockoutEnabled = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        await _email.SendAccountActivatedAsync(user.Email ?? string.Empty, user.FirstName, ct);

        return Ok(new { message = "Compte activé." });
    }

    /// <summary>Locks a user account indefinitely.</summary>
    [HttpPost("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string userId, [FromQuery] string? reason, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        await _email.SendAccountDeactivatedAsync(user.Email ?? string.Empty, user.FirstName, reason ?? "Non spécifié", ct);

        return Ok(new { message = "Compte désactivé." });
    }

    [HttpPost("users/{userId}/reset-password")]
    public async Task<IActionResult> ResetPassword(string userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });
        if (string.IsNullOrWhiteSpace(user.Email))
            return BadRequest(new { error = "Aucun e-mail associé." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var webBase = (_configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
        var resetUrl = $"{webBase}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
        await _email.SendResetPasswordAsync(user.Email, user.FirstName, resetUrl, ct);
        return Ok(new { message = "Lien de réinitialisation envoyé." });
    }

    /// <summary>Approves a pending school/tenant and notifies the owner.</summary>
    [HttpPost("tenants/{tenantId:guid}/approve")]
    public async Task<IActionResult> ApproveTenant(Guid tenantId, CancellationToken ct)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);

        if (tenant is null)
        {
            var user = await _userManager.FindByIdAsync(tenantId.ToString());
            if (user?.TenantId is Guid userTenantId)
                tenant = _db.Tenants.FirstOrDefault(t => t.Id == userTenantId);
        }

        if (tenant is null)
            tenant = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == tenantId.ToString());

        if (tenant is null) return NotFound(new { error = "Tenant introuvable." });

        if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            var owner = _userManager.Users.FirstOrDefault(u => u.TenantId == tenant.Id);
            if (owner is not null)
                tenant.OwnerUserId = owner.Id;
        }

        tenant.Status = TenantStatus.Active;
        tenant.IsPublicProfile = true;
        await _db.SaveChangesAsync(ct);

        var ownerUser = string.IsNullOrWhiteSpace(tenant.OwnerUserId)
            ? null
            : await _userManager.FindByIdAsync(tenant.OwnerUserId);
        if (ownerUser is not null)
        {
            var webBase = (_configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
            var loginUrl = $"{webBase}/login";
            await _email.SendSchoolApprovedAsync(ownerUser.Email ?? string.Empty, ownerUser.FirstName, tenant.Name, loginUrl, ct);
        }

        return Ok(new { message = "Tenant approuvé." });
    }

    [HttpGet("schools")]
    public async Task<IActionResult> GetSchools(CancellationToken ct)
    {
        var schools = await _db.Tenants.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new AdminSchoolDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Country,
                t.City,
                t.Status.ToString(),
                t.Plan.ToString(),
                t.Students.Count,
                0,
                t.CreatedAt))
            .ToListAsync(ct);

        return Ok(schools);
    }

    /// <summary>Returns aggregate counts used by the admin dashboard.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var tutors = (await _userManager.GetUsersInRoleAsync(UserRoles.Tutor)).ToList();
        var parents = (await _userManager.GetUsersInRoleAsync(UserRoles.Parent)).ToList();
        var students = (await _userManager.GetUsersInRoleAsync(UserRoles.Student)).ToList();
        var teachers = (await _userManager.GetUsersInRoleAsync(UserRoles.TeachingAssistant)).ToList();

        var all = tutors.Concat(parents).Concat(students).Concat(teachers)
            .GroupBy(u => u.Id).Select(g => g.First()).ToList();
        var active = all.Count(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
        var inactive = all.Count - active;

        var schools = await _db.Tenants.AsNoTracking().CountAsync(ct);
        var activeCourses = await _db.LessonsForAnyTenant.AsNoTracking()
            .CountAsync(l => l.StartTime >= DateTime.UtcNow.AddDays(-30), ct);

        var countries = await _db.Tenants.AsNoTracking()
            .Where(t => t.Country != null && t.Country != "")
            .GroupBy(t => t.Country!)
            .Select(g => new AdminCountryStatDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(8)
            .ToListAsync(ct);

        var topSchools = await _db.Tenants.AsNoTracking()
            .OrderByDescending(t => t.Students.Count)
            .Take(5)
            .Select(t => new AdminTopSchoolDto(t.Id, t.Name, t.Country, t.Students.Count))
            .ToListAsync(ct);

        var recentUsers = all
            .OrderByDescending(u => u.Id)
            .Take(8)
            .Select(u =>
            {
                var role =
                    tutors.Any(t => t.Id == u.Id) ? UserRoles.Tutor :
                    parents.Any(p => p.Id == u.Id) ? UserRoles.Parent :
                    students.Any(s => s.Id == u.Id) ? UserRoles.Student :
                    UserRoles.TeachingAssistant;
                return new AdminRecentUserDto(
                    u.Id,
                    u.FullName,
                    u.Email ?? string.Empty,
                    role,
                    u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                    null,
                    null);
            })
            .ToList();

        return Ok(new AdminStatsDto(
            all.Count,
            tutors.Count,
            parents.Count,
            students.Count,
            teachers.Count,
            schools,
            activeCourses,
            active,
            inactive,
            countries,
            topSchools,
            recentUsers));
    }

    /// <summary>État de la passerelle e-mail (configuration uniquement — n'envoie rien).</summary>
    [HttpGet("email/status")]
    public IActionResult GetEmailStatus() => Ok(new
    {
        configured = _mailClient.IsConfigured,
        baseUrl = _mailSettings.BaseUrl,
        clientCode = _mailSettings.ClientCode,
        apiKeyPresent = !string.IsNullOrWhiteSpace(_mailSettings.ApiKey),
        webBaseUrl = (_configuration["WebBaseUrl"] ?? "").TrimEnd('/'),
        templates = new[]
        {
            "WELCOME", "CONFIRM_EMAIL_SIMPLE",
            "COURSE_ENROLLMENT_REQUEST", "COURSE_ENROLLMENT_ACCEPTED",
            "INVOICE_READY", "PARENT_PAYMENT_RECEIPT", "PARENT_PAYMENT_OVERDUE",
            "TUTOR_STUDENT_PAYMENT_RECEIVED", "LESSON_REMINDER", "LESSON_SCHEDULED"
        }
    });

    /// <summary>Envoie un e-mail de test WELCOME à l'adresse indiquée (vérification Mail Gateway).</summary>
    [HttpPost("email/test")]
    public async Task<IActionResult> SendTestEmail([FromQuery] string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "Paramètre 'to' requis." });
        if (!_mailClient.IsConfigured)
            return BadRequest(new { error = "Mail Gateway non configuré (Email:ApiKey / Email:BaseUrl)." });

        await _email.SendWelcomeAsync(to.Trim(), "Test", ct);
        return Ok(new { message = $"E-mail WELCOME demandé pour {to.Trim()}." });
    }
}

public sealed record AdminUserDto(
    string Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    string? Phone = null,
    string? Country = null,
    string? City = null,
    string? SchoolName = null,
    Guid? TenantId = null,
    DateTime? CreatedAt = null,
    DateTime? LastLoginAt = null);

public sealed record AdminUserDetailDto(
    string Id,
    string Email,
    string FullName,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    string? Phone,
    string? Country,
    string? City,
    string? SchoolName,
    Guid? TenantId,
    string PreferredLanguage,
    string TimeZone,
    DateTime? CreatedAt,
    DateTime? LastLoginAt);

public sealed record AdminSchoolDto(
    Guid Id,
    string Name,
    string Slug,
    string? Country,
    string? City,
    string Status,
    string Plan,
    int StudentCount,
    int TeacherCount,
    DateTime CreatedAt);

public sealed record AdminCountryStatDto(string Country, int Count);
public sealed record AdminTopSchoolDto(Guid Id, string Name, string? Country, int StudentCount);
public sealed record AdminRecentUserDto(
    string Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? Country,
    string? SchoolName);

public sealed record AdminStatsDto(
    int TotalUsers,
    int TotalTutors,
    int TotalParents,
    int TotalStudents,
    int TotalTeachers,
    int TotalSchools,
    int ActiveCourses,
    int ActiveUsers,
    int InactiveUsers,
    List<AdminCountryStatDto> Countries,
    List<AdminTopSchoolDto> TopSchools,
    List<AdminRecentUserDto> RecentUsers);
