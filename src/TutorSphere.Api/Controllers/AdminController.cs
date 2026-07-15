using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetUsers([FromQuery] string role = "Tutor")
    {
        if (!UserRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Rôle inconnu." });

        var users = await _userManager.GetUsersInRoleAsync(role);
        var result = users
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FullName,
                role,
                u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow))
            .OrderBy(u => u.FullName)
            .ToList();

        return Ok(result);
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

    /// <summary>Approves a pending school/tenant and notifies the owner.
    /// Accepts either the tenant Guid or the tutor user id (admin UI sends user id).</summary>
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

        tenant.Status = TutorSphere.Domain.Enums.TenantStatus.Active;
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

    /// <summary>Returns aggregate counts used by the admin dashboard.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var tutors   = (await _userManager.GetUsersInRoleAsync(UserRoles.Tutor)).ToList();
        var parents  = (await _userManager.GetUsersInRoleAsync(UserRoles.Parent)).ToList();
        var students = (await _userManager.GetUsersInRoleAsync(UserRoles.Student)).ToList();

        var all      = tutors.Concat(parents).Concat(students).ToList();
        var active   = all.Count(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
        var inactive = all.Count - active;

        return Ok(new AdminStatsDto(tutors.Count, parents.Count, students.Count, active, inactive));
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

public sealed record AdminUserDto(string Id, string Email, string FullName, string Role, bool IsActive);
public sealed record AdminStatsDto(int TotalTutors, int TotalParents, int TotalStudents, int ActiveUsers, int InactiveUsers);
