using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Admin;
using TutorSphere.Application.DTOs.PlatformPromo;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Email;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.Services;

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
    private readonly IPlatformPromoService _promoCodes;
    private readonly IAdminUserAccountService _accountDeletion;
    private readonly IAdminUserProvisioningService _provisioning;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IApplicationDbContext db,
        IConfiguration configuration,
        IOptions<MailGatewaySettings> mailSettings,
        MailGatewayClient mailClient,
        IPlatformPromoService promoCodes,
        IAdminUserAccountService accountDeletion,
        IAdminUserProvisioningService provisioning)
    {
        _userManager = userManager;
        _email = email;
        _db = db;
        _configuration = configuration;
        _mailSettings = mailSettings.Value;
        _mailClient = mailClient;
        _promoCodes = promoCodes;
        _accountDeletion = accountDeletion;
        _provisioning = provisioning;
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

        await _email.SendAccountDeactivatedAsync(
            user.Email ?? string.Empty,
            user.FirstName,
            reason ?? Application.Common.EmailCopy.UnspecifiedReason(user.PreferredLanguage),
            ct);

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

    /// <summary>Suppression définitive d'un compte Parent ou Élève (SuperAdmin uniquement).</summary>
    [HttpDelete("users/{userId}")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken ct)
    {
        try
        {
            await _accountDeletion.DeleteParentOrStudentAsync(userId, ct);
            return Ok(new { message = "Compte supprimé définitivement." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("parents")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<AdminCreatedAccountDto>> CreateParent(
        [FromBody] AdminCreateParentRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();
        try
        {
            return Ok(await _provisioning.CreateParentAsync(adminId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("students")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<AdminCreatedAccountDto>> CreateStudent(
        [FromBody] AdminCreateStudentRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();
        try
        {
            return Ok(await _provisioning.CreateStudentAsync(adminId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Crée un enseignant (Tutor) et l'affecte obligatoirement à un groupe d'experts.</summary>
    [HttpPost("teachers")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<AdminCreatedAccountDto>> CreateTeacher(
        [FromBody] AdminCreateTeacherRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();
        try
        {
            return Ok(await _provisioning.CreateTeacherAsync(adminId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
        // Ops override : licence + formation + validation expert considérées complètes.
        tenant.LicenseExpiresAt = DateTime.UtcNow.AddYears(1);
        tenant.OnboardingCompletedAt ??= DateTime.UtcNow;
        tenant.ExpertApprovalStatus = ExpertApprovalStatus.Approved;
        tenant.ExpertApprovedAt ??= DateTime.UtcNow;
        tenant.ExpertApprovalNotes ??= "Approuvé par un administrateur plateforme.";
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

    /// <summary>Returns aggregate counts used by the admin dashboard (données réelles uniquement).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var day30 = now.Date.AddDays(-29);

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
            .CountAsync(l => l.CancelledAt == null && l.StartTime >= now.AddDays(-30) && l.StartTime <= now.AddDays(30), ct);

        var liveLessons = await _db.LessonsForAnyTenant.AsNoTracking()
            .CountAsync(l => l.CancelledAt == null && l.StartTime <= now && l.EndTime >= now, ct);

        var activeSubscriptions = await _db.StudentSubscriptionsForAnyTenant.AsNoTracking()
            .CountAsync(s => s.Status == SubscriptionStatus.Active, ct);

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

        var tenantLookup = await _db.Tenants.AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.Country })
            .ToDictionaryAsync(t => t.Id, ct);

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
                string? country = null;
                string? school = null;
                if (u.TenantId is { } tid && tenantLookup.TryGetValue(tid, out var ten))
                {
                    country = ten.Country;
                    school = ten.Name;
                }

                return new AdminRecentUserDto(
                    u.Id,
                    u.FullName,
                    u.Email ?? string.Empty,
                    role,
                    u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                    country,
                    school);
            })
            .ToList();

        var monthPayments = await _db.PaymentsForAnyTenant.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed
                        && (p.CompletedAt ?? p.CreatedAt) >= monthStart)
            .Select(p => new { p.Amount, p.Currency, p.SubscriptionId, At = p.CompletedAt ?? p.CreatedAt })
            .ToListAsync(ct);

        var monthLicenses = await _db.PlatformLicensePaymentsForAnyTenant.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed
                        && (p.CompletedAt ?? p.CreatedAt) >= monthStart)
            .Select(p => new { p.Amount, p.Currency, At = p.CompletedAt ?? p.CreatedAt })
            .ToListAsync(ct);

        var monthRevenue = monthPayments.Sum(p => p.Amount) + monthLicenses.Sum(p => p.Amount);
        var monthCurrency = monthPayments.Select(p => p.Currency)
            .Concat(monthLicenses.Select(p => p.Currency))
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "CAD";

        var subRevenue = monthPayments.Where(p => p.SubscriptionId != null).Sum(p => p.Amount);
        var licenseRevenue = monthLicenses.Sum(p => p.Amount);
        var otherRevenue = monthPayments.Where(p => p.SubscriptionId == null).Sum(p => p.Amount);
        var paymentBreakdown = new List<AdminPaymentSliceDto>();
        if (monthRevenue > 0)
        {
            void AddSlice(string label, decimal amount)
            {
                if (amount <= 0) return;
                paymentBreakdown.Add(new AdminPaymentSliceDto(
                    label,
                    amount,
                    Math.Round(amount * 100m / monthRevenue, 1)));
            }

            AddSlice("Abonnements", subRevenue);
            AddSlice("Licences plateforme", licenseRevenue);
            AddSlice("Autres", otherRevenue);
        }

        var schoolCreated = await _db.Tenants.AsNoTracking()
            .Where(t => t.CreatedAt >= day30)
            .Select(t => t.CreatedAt.Date)
            .ToListAsync(ct);
        var paymentDays = monthPayments
            .Where(p => p.At >= day30)
            .Select(p => p.At.Date)
            .Concat(monthLicenses.Where(p => p.At >= day30).Select(p => p.At.Date))
            .ToList();

        var dailySignups = Enumerable.Range(0, 30)
            .Select(i =>
            {
                var d = day30.AddDays(i);
                return new AdminDailyCountDto(
                    d,
                    schoolCreated.Count(x => x == d) + paymentDays.Count(x => x == d));
            })
            .ToList();

        var activity = new List<AdminActivityItemDto>();
        var recentSchools = await _db.Tenants.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new { t.Name, t.CreatedAt })
            .ToListAsync(ct);
        foreach (var s in recentSchools)
            activity.Add(new AdminActivityItemDto("École inscrite", s.Name, s.CreatedAt, "#7c5cff"));

        var recentPay = await _db.PaymentsForAnyTenant.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
            .Take(5)
            .Select(p => new { p.Amount, p.Currency, At = p.CompletedAt ?? p.CreatedAt })
            .ToListAsync(ct);
        foreach (var p in recentPay)
            activity.Add(new AdminActivityItemDto(
                "Paiement reçu",
                $"{p.Amount:N0} {p.Currency}",
                p.At,
                "#22c55e"));

        var recentLessons = await _db.LessonsForAnyTenant.AsNoTracking()
            .Where(l => l.CancelledAt == null && l.StartTime <= now.AddHours(2))
            .OrderByDescending(l => l.StartTime)
            .Take(5)
            .Select(l => new { l.Title, l.StartTime })
            .ToListAsync(ct);
        foreach (var l in recentLessons)
            activity.Add(new AdminActivityItemDto("Séance", l.Title, l.StartTime, "#3b82f6"));

        activity = activity.OrderByDescending(a => a.At).Take(10).ToList();

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
            recentUsers,
            monthRevenue,
            monthCurrency,
            liveLessons,
            activeSubscriptions,
            dailySignups,
            paymentBreakdown,
            activity));
    }

    /// <summary>Contrôles de santé réels (DB, e-mail, volumes métier) — pas de données fictives.</summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetPlatformHealth(CancellationToken ct)
    {
        var checks = new List<AdminHealthCheckDto>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var n = await _db.Tenants.AsNoTracking().CountAsync(ct);
            sw.Stop();
            checks.Add(new AdminHealthCheckDto("Base de données", true, $"{n} école(s)", $"{sw.ElapsedMilliseconds} ms"));
        }
        catch (Exception ex)
        {
            sw.Stop();
            checks.Add(new AdminHealthCheckDto("Base de données", false, ex.Message, $"{sw.ElapsedMilliseconds} ms"));
        }

        checks.Add(new AdminHealthCheckDto(
            "API admin",
            true,
            "Endpoint stats / health",
            "OK"));

        checks.Add(new AdminHealthCheckDto(
            "Mail Sender",
            _mailClient.IsConfigured,
            _mailClient.IsConfigured ? (_mailSettings.BaseUrl ?? "configuré") : "Non configuré",
            _mailClient.IsConfigured ? "OK" : "—"));

        try
        {
            var payments = await _db.PaymentsForAnyTenant.AsNoTracking().CountAsync(ct);
            var licenses = await _db.PlatformLicensePaymentsForAnyTenant.AsNoTracking().CountAsync(ct);
            checks.Add(new AdminHealthCheckDto(
                "Paiements",
                true,
                $"{payments} paiement(s), {licenses} licence(s)",
                "OK"));
        }
        catch (Exception ex)
        {
            checks.Add(new AdminHealthCheckDto("Paiements", false, ex.Message, "—"));
        }

        var ok = checks.All(c => c.Ok);
        return Ok(new AdminHealthDto(ok, DateTime.UtcNow, checks));
    }

    /// <summary>État Mail Sender / GiseMailSender (configuration uniquement — n'envoie rien).</summary>
    [HttpGet("email/status")]
    public IActionResult GetEmailStatus() => Ok(new
    {
        provider = "Mail Sender (GiseMailSender / SecureMailGateway)",
        configured = _mailClient.IsConfigured,
        baseUrl = _mailSettings.BaseUrl,
        clientCode = _mailSettings.ClientCode,
        apiKeyPresent = !string.IsNullOrWhiteSpace(_mailSettings.ApiKey),
        endpoint = $"{(_mailSettings.BaseUrl ?? "").TrimEnd('/')}/api/mail/send",
        webBaseUrl = (_configuration["WebBaseUrl"] ?? "").TrimEnd('/'),
        templates = new[]
        {
            "WELCOME", "CONFIRM_EMAIL", "CONFIRM_EMAIL_SIMPLE", "RESET_PASSWORD",
            "COURSE_ENROLLMENT_REQUEST", "COURSE_ENROLLMENT_ACCEPTED",
            "INVOICE_READY", "PARENT_PAYMENT_RECEIPT", "PARENT_PAYMENT_OVERDUE", "PARENT_SUBSCRIPTION_RENEWAL",
            "TUTOR_STUDENT_PAYMENT_RECEIVED", "LESSON_REMINDER", "LESSON_SCHEDULED"
        }
    });

    /// <summary>Envoie un e-mail de test WELCOME via Mail Sender.</summary>
    [HttpPost("email/test")]
    public async Task<IActionResult> SendTestEmail([FromQuery] string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "Paramètre 'to' requis." });
        if (!_mailClient.IsConfigured)
            return BadRequest(new
            {
                error = "Mail Sender non configuré (Email:ApiKey / EMAIL__APIKEY). " +
                        "Client TUTORSPHERE sur https://gisemailsender.gisebs.com — secret TUTORSPHERE_EMAIL_API_KEY."
            });

        await _email.SendWelcomeAsync(to.Trim(), "Test", ct);
        return Ok(new { message = $"E-mail WELCOME demandé via Mail Sender pour {to.Trim()}." });
    }

    [HttpGet("promo-codes")]
    public async Task<IActionResult> ListPromoCodes(CancellationToken ct)
        => Ok(await _promoCodes.ListAsync(ct));

    [HttpPost("promo-codes")]
    public async Task<IActionResult> CreatePromoCodes(
        [FromBody] CreatePlatformPromoCodeRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _promoCodes.CreateAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("promo-codes/{id:guid}")]
    public async Task<IActionResult> SetPromoCodeActive(
        Guid id,
        [FromBody] DeactivatePlatformPromoCodeRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _promoCodes.SetActiveAsync(id, request.IsActive, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
    List<AdminRecentUserDto> RecentUsers,
    decimal MonthRevenue = 0,
    string MonthCurrency = "CAD",
    int LiveLessons = 0,
    int ActiveSubscriptions = 0,
    List<AdminDailyCountDto>? DailySignups = null,
    List<AdminPaymentSliceDto>? PaymentBreakdown = null,
    List<AdminActivityItemDto>? RecentActivity = null);

public sealed record AdminDailyCountDto(DateTime Date, int Count);
public sealed record AdminPaymentSliceDto(string Label, decimal Amount, decimal Percent);
public sealed record AdminActivityItemDto(string Title, string Detail, DateTime At, string Color);
public sealed record AdminHealthCheckDto(string Name, bool Ok, string Detail, string Latency);
public sealed record AdminHealthDto(bool Healthy, DateTime CheckedAt, List<AdminHealthCheckDto> Checks);
