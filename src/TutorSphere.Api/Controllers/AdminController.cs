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
    private readonly ISubscriptionOfferingService _offerings;
    private readonly ITeacherSchoolAdminService _teacherSchools;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IApplicationDbContext db,
        IConfiguration configuration,
        IOptions<MailGatewaySettings> mailSettings,
        MailGatewayClient mailClient,
        IPlatformPromoService promoCodes,
        IAdminUserAccountService accountDeletion,
        IAdminUserProvisioningService provisioning,
        ISubscriptionOfferingService offerings,
        ITeacherSchoolAdminService teacherSchools)
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
        _offerings = offerings;
        _teacherSchools = teacherSchools;
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
            // Tous les rôles : messagerie admin doit pouvoir trouver n'importe quel utilisateur.
            var bags = new List<IList<ApplicationUser>>();
            foreach (var r in UserRoles.All)
                bags.Add(await _userManager.GetUsersInRoleAsync(r));
            users = bags.SelectMany(x => x)
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
            .Select(t => new { t.Id, t.Name, t.Country, t.City, t.OwnerUserId })
            .ToDictionary(t => t.Id);

        // Enseignants sans ApplicationUser.TenantId mais propriétaires d'une école.
        var missingOwnerIds = users
            .Where(u => !u.TenantId.HasValue)
            .Select(u => u.Id)
            .ToList();
        var ownedByUser = _db.Tenants.AsNoTracking()
            .Where(t => t.OwnerUserId != null && t.OwnerUserId != "" && missingOwnerIds.Contains(t.OwnerUserId))
            .Select(t => new { t.Id, t.Name, t.Country, t.City, t.OwnerUserId })
            .ToList()
            .GroupBy(t => t.OwnerUserId!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

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
            Guid? resolvedTenantId = u.TenantId;
            string? schoolName = null;
            string? country = null;
            string? city = null;

            if (resolvedTenantId is Guid tid && tenants.TryGetValue(tid, out var tenant))
            {
                schoolName = tenant.Name;
                country = tenant.Country;
                city = tenant.City;
            }
            else if (ownedByUser.TryGetValue(u.Id, out var owned))
            {
                resolvedTenantId = owned.Id;
                schoolName = owned.Name;
                country = owned.Country;
                city = owned.City;
            }

            result.Add(new AdminUserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FullName,
                userRole,
                u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                u.PhoneNumber,
                country,
                city,
                schoolName,
                resolvedTenantId,
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

    /// <summary>Suppression définitive Parent, Élève ou Enseignant (selon le rôle).</summary>
    [HttpDelete("users/{userId}")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant))
            {
                if (!User.IsInRole(UserRoles.SuperAdmin) && !User.IsInRole(UserRoles.PlatformAdmin))
                    return Forbid();
                await _accountDeletion.DeleteTeacherAsync(userId, ct);
                return Ok(new { message = "Enseignant supprimé. Cours programmés annulés et paiements parents remboursés." });
            }

            if (!User.IsInRole(UserRoles.SuperAdmin))
                return Forbid();

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

    /// <summary>Crée une offre de service pour une école / enseignant existant.</summary>
    [HttpPost("teachers/{tenantId:guid}/offerings")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<TutorSphere.Application.DTOs.SubscriptionOfferings.SubscriptionOfferingDto>> CreateTeacherOffering(
        Guid tenantId,
        [FromBody] TutorSphere.Application.DTOs.SubscriptionOfferings.CreateSubscriptionOfferingRequest? request,
        CancellationToken ct)
    {
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            if (_db.Tenants.FirstOrDefault(t => t.Id == tenantId) is null)
                return NotFound(new { error = "Profil introuvable." });
            return Ok(await _offerings.CreateForTenantAsync(tenantId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Fiche école / enseignant (édition + statut publication).</summary>
    [HttpGet("teachers/by-user/{userId}")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<ActionResult<TeacherSchoolRecordDto>> GetTeacherSchoolByUser(string userId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        Guid? tenantId = user.TenantId;
        if (tenantId is null)
        {
            var owned = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == userId);
            tenantId = owned?.Id;
        }
        if (tenantId is null)
            return NotFound(new { error = "Aucun profil associé à cet utilisateur." });

        var dto = await _teacherSchools.GetByTenantIdAsync(tenantId.Value, ct);
        if (dto is null) return NotFound(new { error = "Profil introuvable." });

        return Ok(dto with
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.PhoneNumber,
            OwnerEmail = user.Email ?? dto.OwnerEmail
        });
    }

    /// <summary>Modifie l'enregistrement enseignant (compte + fiche école / publique).</summary>
    [HttpPut("teachers/by-user/{userId}")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<ActionResult<TeacherSchoolRecordDto>> UpdateTeacherSchoolByUser(
        string userId,
        [FromBody] UpdateTeacherSchoolRecordRequest? request,
        CancellationToken ct)
    {
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Utilisateur introuvable." });

        Guid? tenantId = user.TenantId;
        if (tenantId is null)
            tenantId = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == userId)?.Id;
        if (tenantId is null)
            return NotFound(new { error = "Aucun profil associé à cet utilisateur." });

        try
        {
            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName.Trim();
            if (request.Phone is not null)
                user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

            var updateUser = await _userManager.UpdateAsync(user);
            if (!updateUser.Succeeded)
                return BadRequest(new { error = string.Join("; ", updateUser.Errors.Select(e => e.Description)) });

            var dto = await _teacherSchools.UpdateTenantProfileAsync(tenantId.Value, request, ct);

            if (request.Publish == true && !dto.IsPublicProfile)
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(adminId))
                {
                    await _teacherSchools.PublishPublicProfileAsync(tenantId.Value, adminId, asPlatformAdmin: true, ct);
                    dto = (await _teacherSchools.GetByTenantIdAsync(tenantId.Value, ct))!;
                }
            }

            return Ok(dto with
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.PhoneNumber,
                OwnerEmail = user.Email ?? dto.OwnerEmail
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Publie la fiche publique de l'enseignant (visible recherche / /profil/{slug}).</summary>
    [HttpPost("teachers/{tenantId:guid}/publish-profile")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<ActionResult<PublishTeacherPublicProfileResult>> PublishTeacherProfile(
        Guid tenantId, CancellationToken ct)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();
        try
        {
            return Ok(await _teacherSchools.PublishPublicProfileAsync(tenantId, adminId, asPlatformAdmin: true, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Dépublie la fiche publique (retirée de la recherche et de /profil/{slug}).</summary>
    [HttpPost("teachers/{tenantId:guid}/unpublish-profile")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<ActionResult<PublishTeacherPublicProfileResult>> UnpublishTeacherProfile(
        Guid tenantId, CancellationToken ct)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized();
        try
        {
            return Ok(await _teacherSchools.UnpublishPublicProfileAsync(tenantId, adminId, asPlatformAdmin: true, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Élèves suivant actuellement un cours de l'enseignant (abonnements Active).</summary>
    [HttpGet("teachers/{tenantId:guid}/active-students")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<ActionResult<IReadOnlyList<AdminTeacherActiveStudentDto>>> ListTeacherActiveStudents(
        Guid tenantId, CancellationToken ct)
    {
        var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return NotFound(new { error = "Profil enseignant introuvable." });

        var now = DateTime.UtcNow;
        var rows = await _db.StudentSubscriptionsForAnyTenant.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Id,
                s.StudentId,
                StudentFirst = s.Student.FirstName,
                StudentLast = s.Student.LastName,
                StudentEmail = s.Student.Email,
                ParentFirst = s.Student.Parent != null ? s.Student.Parent.FirstName : null,
                ParentLast = s.Student.Parent != null ? s.Student.Parent.LastName : null,
                s.OfferingId,
                OfferingTitle = s.Offering.Title,
                Subject = s.Offering.Subject,
                Status = s.Status.ToString(),
                s.StartDate,
                s.EndDate,
                s.SessionsRemaining,
                Price = s.Offering.Price,
                Currency = s.Offering.Currency
            })
            .ToListAsync(ct);

        var list = rows.Select(s => new AdminTeacherActiveStudentDto(
            s.Id,
            s.StudentId,
            $"{s.StudentFirst} {s.StudentLast}".Trim(),
            s.StudentEmail,
            string.IsNullOrWhiteSpace($"{s.ParentFirst} {s.ParentLast}".Trim())
                ? null
                : $"{s.ParentFirst} {s.ParentLast}".Trim(),
            s.OfferingId,
            s.OfferingTitle,
            s.Subject,
            s.Status,
            s.StartDate,
            s.EndDate,
            s.SessionsRemaining,
            s.Price,
            s.Currency
        )).ToList();

        // Optionnel : aussi les abonnements encore dans la fenêtre de dates
        _ = now;
        return Ok(list);
    }

    /// <summary>Approves a pending school/tenant and notifies the owner.
    /// Accepte un tenantId OU un userId (GUID Identity) — résout / crée l'école si besoin.</summary>
    [HttpPost("tenants/{tenantId:guid}/approve")]
    public async Task<IActionResult> ApproveTenant(Guid tenantId, CancellationToken ct)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        ApplicationUser? linkedUser = null;

        if (tenant is null)
        {
            linkedUser = await _userManager.FindByIdAsync(tenantId.ToString());
            if (linkedUser?.TenantId is Guid userTenantId)
                tenant = _db.Tenants.FirstOrDefault(t => t.Id == userTenantId);
        }

        if (tenant is null)
            tenant = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == tenantId.ToString());

        // Compte enseignant sans école : créer un profil minimal puis approuver.
        if (tenant is null)
        {
            linkedUser ??= await _userManager.FindByIdAsync(tenantId.ToString());
            if (linkedUser is null)
                return NotFound(new { error = "Tenant / enseignant introuvable." });

            var roles = await _userManager.GetRolesAsync(linkedUser);
            if (!roles.Contains(UserRoles.Tutor) && !roles.Contains(UserRoles.TeachingAssistant))
                return BadRequest(new { error = "Cet utilisateur n'est pas un enseignant — aucun profil école à approuver." });

            var baseSlug = Slugify(linkedUser.FullName);
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = "enseignant";
            var slug = baseSlug;
            var n = 0;
            while (_db.Tenants.Any(t => t.Slug == slug))
            {
                n++;
                slug = $"{baseSlug}-{n}";
            }

            tenant = new Domain.Entities.Tenant
            {
                Name = string.IsNullOrWhiteSpace(linkedUser.FullName) ? linkedUser.Email ?? "Enseignant" : linkedUser.FullName,
                Slug = slug,
                OwnerUserId = linkedUser.Id,
                Status = TenantStatus.PendingValidation,
                Language = string.IsNullOrWhiteSpace(linkedUser.PreferredLanguage) ? "fr" : linkedUser.PreferredLanguage,
                Currency = "XAF",
                Branding = new Domain.Entities.TenantBranding()
            };
            _db.Add(tenant);
            await _db.SaveChangesAsync(ct);

            linkedUser.TenantId = tenant.Id;
            await _userManager.UpdateAsync(linkedUser);
        }

        if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            var owner = _userManager.Users.FirstOrDefault(u => u.TenantId == tenant.Id);
            if (owner is not null)
                tenant.OwnerUserId = owner.Id;
        }

        // Relier le user au tenant s'il manquait.
        if (!string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            var ownerUser = await _userManager.FindByIdAsync(tenant.OwnerUserId);
            if (ownerUser is not null && ownerUser.TenantId != tenant.Id)
            {
                ownerUser.TenantId = tenant.Id;
                await _userManager.UpdateAsync(ownerUser);
            }
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

        var notifyUser = string.IsNullOrWhiteSpace(tenant.OwnerUserId)
            ? null
            : await _userManager.FindByIdAsync(tenant.OwnerUserId);
        if (notifyUser is not null)
        {
            var webBase = (_configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
            var loginUrl = $"{webBase}/login";
            await _email.SendSchoolApprovedAsync(notifyUser.Email ?? string.Empty, notifyUser.FirstName, tenant.Name, loginUrl, ct);
        }

        return Ok(new { message = "Profil enseignant approuvé.", tenantId = tenant.Id });
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = value.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        return sb.ToString().Trim('-');
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

    /// <summary>Suppression définitive d'un profil enseignant (tenant). SuperAdmin uniquement.</summary>
    [HttpDelete("schools/{tenantId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteSchool(Guid tenantId, CancellationToken ct)
    {
        try
        {
            await _accountDeletion.DeleteTenantAsync(tenantId, ct);
            return Ok(new { message = "Profil supprimé. Cours programmés annulés et paiements parents remboursés." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
            activity.Add(new AdminActivityItemDto("Profil inscrit", s.Name, s.CreatedAt, "#7c5cff"));

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
            checks.Add(new AdminHealthCheckDto("Base de données", true, $"{n} profil(s)", $"{sw.ElapsedMilliseconds} ms"));
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
            "INVOICE_READY", "PARENT_PAYMENT_RECEIPT", "PARENT_PAYMENT_REFUNDED", "PARENT_PAYMENT_OVERDUE", "PARENT_SUBSCRIPTION_RENEWAL",
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
