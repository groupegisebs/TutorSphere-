using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/membership")]
public class ExpertMembershipController(
    IExpertMembershipGovernanceService membership,
    IUserContactLookup contacts,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);
    private bool AsPlatformActAs => groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;

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

    [HttpPost("invites/{inviteId:guid}/vote")]
    [Authorize(Roles = UserRoles.Expert)]
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
