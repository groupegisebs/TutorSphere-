using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin/expert-groups")]
[Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertGroupsController : ControllerBase
{
    private readonly IExpertGroupService _groups;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IAppUrlProvider _urls;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ExpertGroupsController> _logger;

    public ExpertGroupsController(
        IExpertGroupService groups,
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IAppUrlProvider urls,
        IWebHostEnvironment env,
        ILogger<ExpertGroupsController> logger)
    {
        _groups = groups;
        _userManager = userManager;
        _email = email;
        _urls = urls;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpertGroupDto>>> List(CancellationToken ct)
        => Ok(await _groups.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpertGroupDto>> Get(Guid id, CancellationToken ct)
    {
        var g = await _groups.GetByIdAsync(id, ct);
        return g is null ? NotFound(new { error = "Groupe introuvable." }) : Ok(g);
    }

    [HttpPost]
    public async Task<ActionResult<ExpertGroupDto>> Create([FromBody] CreateExpertGroupRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _groups.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExpertGroupDto>> Update(Guid id, [FromBody] UpdateExpertGroupRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _groups.UpdateAsync(id, request, ct));
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
            // Contrainte FK inattendue (ex. nouvelle relation ajoutée sans ON DELETE CASCADE/SET NULL) :
            // on log les détails, mais on renvoie un message exploitable côté admin plutôt qu'un 500 muet.
            _logger.LogError(ex, "Échec suppression groupe d'experts {GroupId} : contrainte base de données.", id);
            return BadRequest(new
            {
                error = "Impossible de supprimer ce groupe : des données (enseignants, invitations, écoles…) y sont encore liées. " +
                         "Vous pouvez le désactiver à la place."
            });
        }
    }

    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> UploadLogo(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });

        var group = await _groups.GetByIdAsync(id, ct);
        if (group is null)
            return NotFound(new { error = "Groupe introuvable." });

        var uploadsRoot = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);
        var safeFileName = $"expert-group-{id:N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsRoot, safeFileName);
        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        var url = $"/uploads/{safeFileName}";
        var updated = await _groups.UpdateAsync(id, new UpdateExpertGroupRequest(
            group.Name, group.ContactEmail, group.ContactPhone, url, group.IsActive, group.ContactName), ct);
        return Ok(new { logoUrl = updated.LogoUrl });
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

            if (!await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                await _userManager.AddToRoleAsync(user, UserRoles.Expert);

            var member = await _groups.AddMemberAsync(id, user.Id, ct);
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

            if (!await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                await _userManager.AddToRoleAsync(user, UserRoles.Expert);

            var member = await _groups.AddMemberAsync(id, user.Id, ct);
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
}
