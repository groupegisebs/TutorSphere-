using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Api;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin/expert-groups")]
[Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertGroupsController : ControllerBase
{
    private readonly IExpertGroupService _groups;
    private readonly IExpertGroupManagerService _managers;
    private readonly IGroupAdminChatService _chat;
    private readonly IExpertMembershipGovernanceService _membership;
    private readonly IExpertIdentityActions _identity;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IAppUrlProvider _urls;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ExpertGroupsController> _logger;

    public ExpertGroupsController(
        IExpertGroupService groups,
        IExpertGroupManagerService managers,
        IGroupAdminChatService chat,
        IExpertMembershipGovernanceService membership,
        IExpertIdentityActions identity,
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IAppUrlProvider urls,
        IWebHostEnvironment env,
        ILogger<ExpertGroupsController> logger)
    {
        _groups = groups;
        _managers = managers;
        _chat = chat;
        _membership = membership;
        _identity = identity;
        _userManager = userManager;
        _email = email;
        _urls = urls;
        _env = env;
        _logger = logger;
    }

    private string? AdminUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpertGroupDto>>> List(CancellationToken ct)
    {
        var list = await _groups.ListAsync(ct);
        var enriched = new List<ExpertGroupDto>();
        foreach (var g in list)
            enriched.Add(await EnrichManagerAsync(g, ct));
        return Ok(enriched);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpertGroupDto>> Get(Guid id, CancellationToken ct)
    {
        var g = await _groups.GetByIdAsync(id, ct);
        if (g is null) return NotFound(new { error = "Groupe introuvable." });
        return Ok(await EnrichManagerAsync(g, ct));
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<ExpertGroupDto>> Create([FromBody] CreateExpertGroupRequest request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        Guid? createdId = null;
        try
        {
            var created = await _groups.CreateAsync(request, ct);
            createdId = created.Id;
            var managerUser = await ResolveOrCreateManagerUserAsync(request, ct);
            if (managerUser is null)
            {
                await TryCompensateCreateAsync(created.Id, ct);
                return BadRequest(new { error = "Impossible de résoudre le Responsable (e-mail ou utilisateur requis)." });
            }

            await _groups.AddMemberAsync(created.Id, managerUser.Id, AdminUserId, ct: ct);
            await _identity.EnsureGroupManagerRoleAsync(managerUser.Id, ct);

            await _managers.AppointAsync(created.Id, AdminUserId, managerUser.Id, new AppointGroupManagerRequest(
                ExistingUserId: managerUser.Id,
                Email: managerUser.Email,
                FirstName: request.ManagerFirstName ?? managerUser.FirstName,
                LastName: request.ManagerLastName ?? managerUser.LastName,
                Phone: request.ManagerPhone ?? request.ContactPhone,
                FunctionTitle: request.ManagerFunctionTitle,
                MandateStartsAtUtc: request.ManagerMandateStartsAtUtc), ct);

            // Sync contact mirror + activate (LogoUrl null = conserver)
            var group = await _groups.GetByIdAsync(created.Id, ct);
            if (group is not null)
            {
                await _groups.UpdateAsync(created.Id, new UpdateExpertGroupRequest(
                    group.Name,
                    managerUser.Email,
                    request.ManagerPhone ?? request.ContactPhone,
                    LogoUrl: null,
                    IsActive: true,
                    ContactName: managerUser.FullName,
                    Description: request.Description,
                    CountryCode: group.CountryCode), ct);
            }

            await SendExpertLoginCredentialsAsync(managerUser, created.Name, ct);
            var refreshed = await _groups.GetByIdAsync(created.Id, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id },
                refreshed is null ? created : await EnrichManagerAsync(refreshed, ct));
        }
        catch (InvalidOperationException ex)
        {
            if (createdId is Guid id)
                await TryCompensateCreateAsync(id, ct);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpertGroupDto>> Update(Guid id, [FromBody] UpdateExpertGroupRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await EnrichManagerAsync(await _groups.UpdateAsync(id, request, ct), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _groups.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            _logger.LogError(ex, "Échec suppression groupe d'experts {GroupId} : contrainte base de données.", id);
            return BadRequest(new
            {
                error = "Impossible de supprimer ce groupe : des données y sont encore liées. Utilisez Archiver."
            });
        }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        try
        {
            var previous = await _managers.GetActiveManagerAsync(id, ct);
            await _groups.ArchiveAsync(id, ct);
            if (previous is not null)
                await _identity.RemoveGroupManagerRoleAsync(previous.UserId, ct);
            return Ok(new { message = "Groupe archivé." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/manager")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<ExpertGroupDto>> TransferManager(
        Guid id, [FromBody] TransferGroupManagerRequest request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            var previous = await _managers.GetActiveManagerAsync(id, ct);
            var user = await _userManager.FindByIdAsync(request.NewManagerUserId)
                ?? throw new InvalidOperationException("Utilisateur introuvable.");

            var members = await _groups.ListMembersAsync(id, ct);
            if (!members.Any(m => m.UserId == user.Id))
                await _groups.AddMemberAsync(id, user.Id, AdminUserId, ct: ct);

            await _managers.AppointAsync(id, AdminUserId, user.Id, new AppointGroupManagerRequest(
                ExistingUserId: user.Id,
                Email: user.Email,
                Phone: request.Phone,
                FunctionTitle: request.FunctionTitle,
                MandateStartsAtUtc: request.MandateStartsAtUtc,
                IsTemporary: request.IsTemporary), ct);

            await _identity.EnsureGroupManagerRoleAsync(user.Id, ct);
            if (previous is not null && previous.UserId != user.Id)
                await _identity.RemoveGroupManagerRoleAsync(previous.UserId, ct);

            var group = await _groups.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException("Groupe introuvable.");
            await _groups.UpdateAsync(id, new UpdateExpertGroupRequest(
                group.Name, user.Email, request.Phone ?? group.ContactPhone, LogoUrl: null, true,
                user.FullName, group.Description, group.CountryCode));

            return Ok(await EnrichManagerAsync((await _groups.GetByIdAsync(id, ct))!, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/manager/suspend")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<IActionResult> SuspendManager(Guid id, [FromBody] SuspendGroupManagerRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            var previous = await _managers.GetActiveManagerAsync(id, ct);
            await _managers.SuspendActiveMandateAsync(id, AdminUserId, request?.Reason, ct);
            if (previous is not null)
                await _identity.RemoveGroupManagerRoleAsync(previous.UserId, ct);
            return Ok(new { message = "Mandat du Responsable suspendu." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/contact")]
    public async Task<ActionResult<object>> ContactGroup(
        Guid id, [FromBody] CreateGroupAdminConversationRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            var manager = await _managers.GetActiveManagerAsync(id, ct)
                ?? throw new InvalidOperationException("Aucun Responsable actif pour ouvrir le canal.");

            var subject = string.IsNullOrWhiteSpace(request?.Subject)
                ? "Contact Super Admin"
                : request!.Subject;
            var message = string.IsNullOrWhiteSpace(request?.Message)
                ? "Conversation ouverte depuis le Control Center."
                : request!.Message;

            var conversation = await _chat.OpenOrCreateForGroupAsync(
                id,
                manager.UserId,
                new CreateGroupAdminConversationRequest(
                    subject,
                    request?.Category ?? GroupAdminConversationCategory.Administrative,
                    request?.Priority ?? GroupAdminConversationPriority.Normal,
                    message),
                ct);

            // First message attributed to manager for routing; admin can reply next.
            return Ok(new { conversationId = conversation.Id, reference = conversation.Reference });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<ExpertGroupDto> EnrichManagerAsync(ExpertGroupDto g, CancellationToken ct = default)
    {
        g = await SanitizeMissingLogoAsync(g, ct);

        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(g.ManagerUserId))
            user = await _userManager.FindByIdAsync(g.ManagerUserId);

        if (user is null && !string.IsNullOrWhiteSpace(g.ContactEmail))
            user = await _userManager.FindByEmailAsync(g.ContactEmail.Trim());

        if (user is null)
            return g;

        return g with
        {
            ManagerUserId = g.ManagerUserId ?? user.Id,
            ManagerFullName = string.IsNullOrWhiteSpace(user.FullName) ? g.ManagerFullName ?? g.ContactName : user.FullName,
            ManagerEmail = user.Email ?? g.ManagerEmail ?? g.ContactEmail,
            ManagerPhone = g.ManagerPhone ?? g.ContactPhone,
            ContactName = string.IsNullOrWhiteSpace(user.FullName) ? g.ContactName : user.FullName,
            ContactEmail = user.Email ?? g.ContactEmail,
            ContactPhone = g.ManagerPhone ?? g.ContactPhone
        };
    }

    /// <summary>
    /// DB may still point at /uploads/expert-group-….png after a volume reset or failed write.
    /// Hide broken URLs (stops browser 404 spam) and clear the stale path once.
    /// </summary>
    private async Task<ExpertGroupDto> SanitizeMissingLogoAsync(ExpertGroupDto g, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(g.LogoUrl))
            return g;

        var fileName = Path.GetFileName(g.LogoUrl.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            return g with { LogoUrl = null };

        if (UploadsPaths.FindExistingFile(_env, fileName) is not null)
            return g;

        _logger.LogWarning(
            "Logo groupe {GroupId} introuvable sur disque ({LogoUrl}). Référence effacée — re-uploader le logo.",
            g.Id, g.LogoUrl);

        try
        {
            await _groups.SetLogoUrlAsync(g.Id, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'effacer LogoUrl obsolète pour le groupe {GroupId}.", g.Id);
        }

        return g with { LogoUrl = null };
    }

    private async Task TryCompensateCreateAsync(Guid groupId, CancellationToken ct)
    {
        try
        {
            var previous = await _managers.GetActiveManagerAsync(groupId, ct);
            await _groups.DeleteAsync(groupId, ct);
            if (previous is not null)
                await _identity.RemoveGroupManagerRoleAsync(previous.UserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Compensation création groupe {GroupId} échouée — un brouillon orphelin peut rester.",
                groupId);
        }
    }

    private async Task<ApplicationUser?> ResolveOrCreateManagerUserAsync(
        CreateExpertGroupRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ManagerUserId))
            return await _userManager.FindByIdAsync(request.ManagerUserId.Trim());

        var email = (request.ManagerEmail ?? request.ContactEmail)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        if (!request.CreateManagerAccount)
            return null;

        var firstName = (request.ManagerFirstName ?? string.Empty).Trim();
        var lastName = (request.ManagerLastName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new InvalidOperationException("Prénom et nom du Responsable requis pour créer le compte.");

        var password = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.ManagerPhone) ? null : request.ManagerPhone.Trim(),
            EmailConfirmed = true,
            MustChangePassword = true
        };
        var create = await _userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));
        return user;
    }

    private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"
    };

    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadLogo(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Logo trop volumineux (max. 5 Mo)." });

        var group = await _groups.GetByIdAsync(id, ct);
        if (group is null)
            return NotFound(new { error = "Groupe introuvable." });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedLogoExtensions.Contains(extension))
        {
            // Infer from Content-Type when the browser omits an extension (common with some PNGs).
            extension = file.ContentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ""
            };
        }

        if (string.IsNullOrWhiteSpace(extension) || !AllowedLogoExtensions.Contains(extension))
            return BadRequest(new { error = "Format non supporté. Utilisez PNG, JPG, GIF, WebP ou SVG." });

        var uploadsRoot = UploadsPaths.GetRoot(_env);
        // Stable name per group so re-uploads overwrite; keep a real image extension for MIME mapping.
        var safeFileName = $"expert-group-{id:N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, safeFileName);
        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        var url = $"/uploads/{safeFileName}";
        try
        {
            var updated = await _groups.SetLogoUrlAsync(id, url, ct);
            return Ok(new { logoUrl = updated.LogoUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ExpertGroupMemberDto>>> ListMembers(Guid id, CancellationToken ct)
    {
        try
        {
            var members = await _groups.ListMembersAsync(id, ct);
            var enriched = new List<ExpertGroupMemberDto>();
            foreach (var m in members)
            {
                var user = await _userManager.FindByIdAsync(m.UserId);
                enriched.Add(m with
                {
                    Email = user?.Email ?? string.Empty,
                    FullName = user?.FullName ?? m.UserId
                });
            }
            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<ExpertGroupMemberDto>> AddMember(
        Guid id, [FromBody] AddExpertMemberRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user is null)
                return NotFound(new { error = "Utilisateur introuvable." });

            var group = await _groups.GetByIdAsync(id, ct);
            if (group is null)
                return NotFound(new { error = "Groupe introuvable." });

            // Ajout de l'appartenance au groupe avant l'attribution du rôle Expert : si l'utilisateur
            // appartient déjà à un autre groupe (règle « un seul groupe par expert »), on échoue sans
            // avoir touché à ses rôles.
            var member = await _groups.AddMemberAsync(id, user.Id, AdminUserId, ct: ct);

            if (!await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                await _userManager.AddToRoleAsync(user, UserRoles.Expert);

            var credentialsSent = await SendExpertLoginCredentialsAsync(user, group.Name, ct);
            return Ok(member with
            {
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                CredentialsSent = credentialsSent,
                NotificationSent = credentialsSent
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ajoute un expert par e-mail. Dans tous les cas, envoie EXPERT_INVITE avec
    /// URL /login/expert, e-mail et mot de passe temporaire (changement obligatoire).
    /// Invite=true : crée le compte si besoin (prénom/nom requis).
    /// Invite=false : compte existant uniquement.
    /// </summary>
    [HttpPost("{id:guid}/members/by-email")]
    public async Task<ActionResult<ExpertGroupMemberDto>> AddMemberByEmail(
        Guid id, [FromBody] AddExpertByEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "E-mail requis." });

        var group = await _groups.GetByIdAsync(id, ct);
        if (group is null)
            return NotFound(new { error = "Groupe introuvable." });

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        var accountCreated = false;

        try
        {
            if (request.Invite)
            {
                if (user is null)
                {
                    var firstName = (request.FirstName ?? string.Empty).Trim();
                    var lastName = (request.LastName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                        return BadRequest(new { error = "Prénom et nom requis pour créer un compte expert." });

                    var createPassword = GenerateTemporaryPassword();
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        EmailConfirmed = true,
                        MustChangePassword = true
                    };

                    var create = await _userManager.CreateAsync(user, createPassword);
                    if (!create.Succeeded)
                        return BadRequest(new { error = string.Join("; ", create.Errors.Select(e => e.Description)) });

                    accountCreated = true;
                    // Mot de passe déjà défini à la création — envoi ci-dessous après reset unifié.
                    _logger.LogInformation(
                        "Compte expert créé pour invitation (userId={UserId}, groupId={GroupId}).",
                        user.Id, id);
                }
                else if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
                {
                    if (!string.IsNullOrWhiteSpace(request.FirstName))
                        user.FirstName = request.FirstName.Trim();
                    if (!string.IsNullOrWhiteSpace(request.LastName))
                        user.LastName = request.LastName.Trim();
                    await _userManager.UpdateAsync(user);
                }
            }
            else if (user is null)
            {
                return NotFound(new { error = "Aucun compte avec cet e-mail. Utilisez « Inviter » pour créer le compte." });
            }

            // Ajout de l'appartenance au groupe avant l'attribution du rôle Expert : si l'utilisateur
            // appartient déjà à un autre groupe (règle « un seul groupe par expert »), on échoue sans
            // avoir touché à ses rôles.
            var member = await _groups.AddMemberAsync(id, user.Id, AdminUserId, ct: ct);

            if (!await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                await _userManager.AddToRoleAsync(user, UserRoles.Expert);

            var credentialsSent = await SendExpertLoginCredentialsAsync(user, group.Name, ct);

            return Ok(member with
            {
                Email = user.Email ?? email,
                FullName = user.FullName,
                AccountCreated = accountCreated,
                CredentialsSent = credentialsSent,
                NotificationSent = credentialsSent
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken ct)
    {
        try
        {
            await _groups.RemoveMemberAsync(id, userId, ct);

            var allGroups = await _groups.ListAsync(ct);
            var stillInAny = false;
            foreach (var g in allGroups)
            {
                var members = await _groups.ListMembersAsync(g.Id, ct);
                if (members.Any(m => m.UserId == userId))
                {
                    stillInAny = true;
                    break;
                }
            }

            if (!stillInAny)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user is not null && await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                    await _userManager.RemoveFromRoleAsync(user, UserRoles.Expert);
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("~/api/admin/pending-teacher-approvals")]
    public async Task<ActionResult<IReadOnlyList<PendingTeacherDto>>> PendingTeachers(
        [FromServices] IExpertApprovalService approvals, CancellationToken ct)
        => Ok(await approvals.ListAllPendingAsync(ct));

    /// <summary>
    /// Régénère un MDP temporaire et envoie EXPERT_INVITE (URL expert + e-mail + MDP).
    /// </summary>
    private async Task<bool> SendExpertLoginCredentialsAsync(
        ApplicationUser user, string groupName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        var temporaryPassword = GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!reset.Succeeded)
        {
            _logger.LogWarning(
                "Impossible de régénérer le MDP temporaire pour l'expert {UserId}: {Errors}",
                user.Id, string.Join("; ", reset.Errors.Select(e => e.Description)));
            return false;
        }

        user.MustChangePassword = true;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        var loginUrl = $"{_urls.WebBaseUrl.TrimEnd('/')}/login/expert";
        await _email.SendExpertInviteAsync(
            user.Email,
            string.IsNullOrWhiteSpace(user.FirstName) ? user.Email : user.FirstName,
            temporaryPassword,
            loginUrl,
            groupName,
            ct);
        return true;
    }

    [HttpGet("~/api/admin/expert-membership-invites")]
    public async Task<ActionResult<IReadOnlyList<ExpertMembershipInviteDto>>> ListMembershipInvites(
        [FromQuery] Guid? groupId,
        CancellationToken ct)
    {
        var list = await _membership.ListForAdminAsync(groupId, ct);
        return Ok(list);
    }

    [HttpPost("~/api/admin/expert-membership-invites/{inviteId:guid}/force-approve")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> ForceApprove(
        Guid inviteId, [FromBody] AdminExpertMembershipActionRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            return Ok(await _membership.AdminForceApproveAsync(AdminUserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("~/api/admin/expert-membership-invites/{inviteId:guid}/force-reject")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> ForceReject(
        Guid inviteId, [FromBody] AdminExpertMembershipActionRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            return Ok(await _membership.AdminForceRejectAsync(AdminUserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("~/api/admin/expert-membership-invites/{inviteId:guid}/cancel")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> CancelInvite(
        Guid inviteId, [FromBody] AdminExpertMembershipActionRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            return Ok(await _membership.AdminCancelAsync(AdminUserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("~/api/admin/expert-membership-invites/{inviteId:guid}/extend")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> ExtendInvite(
        Guid inviteId, [FromBody] AdminExpertMembershipActionRequest request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            return Ok(await _membership.AdminExtendAsync(AdminUserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("~/api/admin/expert-membership-invites/{inviteId:guid}/validate")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> ValidateSmallGroup(
        Guid inviteId, [FromBody] AdminExpertMembershipActionRequest? request, CancellationToken ct)
    {
        if (AdminUserId is null) return Unauthorized();
        try
        {
            return Ok(await _membership.AdminValidateSmallGroupAsync(AdminUserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Mot de passe temporaire respectant Identity.
    /// Évite $ et # (casse souvent le HTML e-mail / le copier-coller).
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@%*?";
        Span<char> code = stackalloc char[12];
        code[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        code[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        code[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        code[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        const string all = upper + lower + digits + symbols;
        for (var i = 4; i < code.Length; i++)
            code[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = code.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (code[i], code[j]) = (code[j], code[i]);
        }

        return new string(code);
    }

    /// <summary>Active le mode administrateur suppléant pour ce groupe (Control Center → Espace Responsable).</summary>
    [HttpPost("{id:guid}/act-as")]
    public async Task<ActionResult<object>> ActAsGroupAdmin(Guid id, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(id, ct);
        if (group is null) return NotFound(new { error = "Groupe introuvable." });
        if (!group.IsActive)
            return BadRequest(new { error = "Le groupe est inactif." });

        return Ok(new
        {
            groupId = group.Id,
            groupName = group.Name,
            countryCode = group.CountryCode,
            isInternational = group.IsInternational,
            portalPath = "/group-admin/dashboard"
        });
    }
}
