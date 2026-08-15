using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/membership")]
public class ExpertMembershipController(
    IExpertMembershipGovernanceService membership,
    IExpertGroupMemberAdminService memberAdmin,
    IUserContactLookup contacts,
    IGroupAdminAccessService groupAccess,
    UserManager<ApplicationUser> users) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);
    private bool AsPlatformActAs => groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
    private bool CanManageRestrictedRoles => groupAccess.IsPlatformAdmin(User);

    private const string ManagerOrPlatform =
        $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}";

    [HttpPost("invites")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> CreateInvite(
        [FromBody] CreateExpertMembershipInviteRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await membership.CreateInviteAsync(
                UserId, request, ct, asPlatformAdmin: AsPlatformActAs, actAsGroupId: ActAsGroupId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("invites")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<ExpertMembershipInviteDto>>> ListInvites(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await membership.ListForExpertAsync(
                UserId, ct, asPlatformAdmin: AsPlatformActAs, actAsGroupId: ActAsGroupId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("members")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<ExpertGroupMemberListItemDto>>> ListMembers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var list = await membership.ListActiveMembersAsync(
                UserId, ct, asPlatformAdmin: AsPlatformActAs, actAsGroupId: ActAsGroupId);
            var enriched = new List<ExpertGroupMemberListItemDto>();
            foreach (var m in list)
            {
                var c = await contacts.GetAsync(m.UserId, ct);
                enriched.Add(m with
                {
                    Email = c?.Email ?? "",
                    FullName = c?.DisplayName ?? m.UserId
                });
            }
            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("directory")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<GroupMemberDirectoryDto>> Directory(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var dir = await memberAdmin.GetDirectoryAsync(UserId, ct, AsPlatformActAs, ActAsGroupId);
            var items = new List<GroupMemberDirectoryItemDto>();
            foreach (var m in dir.Items)
            {
                if (m.Kind != "member" || string.IsNullOrWhiteSpace(m.UserId))
                {
                    items.Add(m);
                    continue;
                }
                var c = await contacts.GetAsync(m.UserId, ct);
                var user = await users.FindByIdAsync(m.UserId);
                items.Add(m with
                {
                    Email = c?.Email ?? m.Email,
                    FullName = c?.DisplayName ?? m.FullName,
                    Phone = string.IsNullOrWhiteSpace(user?.PhoneNumber) ? m.Phone : user!.PhoneNumber
                });
            }
            return Ok(dir with { Items = items });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("members/{userId}/activity")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<GroupMemberActivityDto>> Activity(string userId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await memberAdmin.GetActivityAsync(UserId, userId, ct, AsPlatformActAs, ActAsGroupId));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("members/{userId}/role")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<IActionResult> UpdateRole(string userId, [FromBody] UpdateGroupMemberRoleRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            await memberAdmin.UpdateRoleAsync(
                UserId, userId, request, ct, AsPlatformActAs || CanManageRestrictedRoles, ActAsGroupId);
            return Ok(new { message = "Rôle mis à jour." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("roles")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<IReadOnlyList<GroupDefinedRoleDto>>> ListRoles(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await memberAdmin.ListDefinedRolesAsync(
                UserId, ct, AsPlatformActAs || CanManageRestrictedRoles, ActAsGroupId));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("roles")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<GroupDefinedRoleDto>> CreateRole(
        [FromBody] CreateGroupDefinedRoleRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await memberAdmin.CreateDefinedRoleAsync(
                UserId, request, ct, AsPlatformActAs || CanManageRestrictedRoles, ActAsGroupId));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("members/{userId}/permissions")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<IActionResult> UpdatePermissions(string userId, [FromBody] UpdateGroupMemberPermissionsRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await memberAdmin.UpdatePermissionsAsync(UserId, userId, request?.Permissions ?? [], ct, AsPlatformActAs, ActAsGroupId);
            return Ok(new { message = "Permissions enregistrées." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("members/{userId}/suspend")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<IActionResult> Suspend(string userId, [FromBody] SuspendGroupMemberRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await memberAdmin.SuspendAsync(UserId, userId, request?.Reason ?? "", ct, AsPlatformActAs, ActAsGroupId);
            return Ok(new { message = "Membre suspendu." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("members/{userId}/reactivate")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<IActionResult> Reactivate(string userId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await memberAdmin.ReactivateAsync(UserId, userId, ct, AsPlatformActAs, ActAsGroupId);
            return Ok(new { message = "Membre réactivé." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("members/{userId}/removal-check")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<GroupMemberRemovalCheckDto>> RemovalCheck(string userId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await memberAdmin.PreviewRemoveAsync(UserId, userId, ct, AsPlatformActAs, ActAsGroupId));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("members/{userId}/remove")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<IActionResult> Remove(string userId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await memberAdmin.RemoveAsync(UserId, userId, ct, AsPlatformActAs, ActAsGroupId);
            return Ok(new { message = "Membre retiré du groupe." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("invites/{inviteId:guid}/vote")]
    [Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager}")]
    public async Task<ActionResult<ExpertMembershipInviteDto>> Vote(
        Guid inviteId,
        [FromBody] CastExpertMembershipVoteRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await membership.CastVoteAsync(UserId, inviteId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("public/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ExpertMembershipInvitePublicDto>> GetPublic(string token, CancellationToken ct)
    {
        try
        {
            return Ok(await membership.GetPublicInviteAsync(token, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("public/submit")]
    [AllowAnonymous]
    public async Task<ActionResult<ExpertMembershipInviteDto>> Submit(
        [FromBody] SubmitExpertMembershipCandidacyRequest? request,
        CancellationToken ct)
    {
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await membership.SubmitCandidacyAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("public/decline")]
    [AllowAnonymous]
    public async Task<IActionResult> Decline([FromBody] TokenBody? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Token))
            return BadRequest(new { error = "Jeton requis." });
        try
        {
            await membership.DeclineInviteAsync(body.Token, ct);
            return Ok(new { message = "Invitation refusée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record TokenBody(string? Token);
}
