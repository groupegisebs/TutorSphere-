using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IWebHostEnvironment _env;

    public ExpertGroupsController(
        IExpertGroupService groups,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _groups = groups;
        _userManager = userManager;
        _env = env;
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
            group.Name, group.ContactEmail, group.ContactPhone, url, group.IsActive), ct);
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

            if (!await _userManager.IsInRoleAsync(user, UserRoles.Expert))
                await _userManager.AddToRoleAsync(user, UserRoles.Expert);

            var member = await _groups.AddMemberAsync(id, user.Id, ct);
            return Ok(member with
            {
                Email = user.Email ?? string.Empty,
                FullName = user.FullName
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/members/by-email")]
    public async Task<ActionResult<ExpertGroupMemberDto>> AddMemberByEmail(
        Guid id, [FromBody] AddExpertByEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "E-mail requis." });

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return NotFound(new { error = "Aucun compte avec cet e-mail. Créez d'abord l'utilisateur." });

        return await AddMember(id, new AddExpertMemberRequest(user.Id), ct);
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
}

public record AddExpertByEmailRequest(string Email);
