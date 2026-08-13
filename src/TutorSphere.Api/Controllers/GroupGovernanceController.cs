using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api")]
public class GroupGovernanceController : ControllerBase
{
    private readonly IGroupOfferService _offers;
    private readonly IGroupAdminChatService _chat;
    private readonly ITeacherInterestService _interest;
    private readonly IExpertGroupManagerService _managers;

    public GroupGovernanceController(
        IGroupOfferService offers,
        IGroupAdminChatService chat,
        ITeacherInterestService interest,
        IExpertGroupManagerService managers)
    {
        _offers = offers;
        _chat = chat;
        _interest = interest;
        _managers = managers;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("public/teacher-interest")]
    [AllowAnonymous]
    public async Task<ActionResult<TeacherInterestRequestDto>> SubmitInterest(
        [FromBody] SubmitTeacherInterestRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await _interest.SubmitAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/group-offers")]
    [Authorize(Roles = UserRoles.Expert)]
    public async Task<ActionResult<IReadOnlyList<GroupOfferListItemDto>>> ListOffers(
        [FromServices] IExpertGroupService groups, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var membership = groups; // resolve via manager / member group
        var all = await groups.ListAsync(ct);
        // Find the caller's group via active membership in manager or list members — use mandates first
        Guid? groupId = null;
        foreach (var g in all)
        {
            if (_managers.IsActiveManager(UserId, g.Id))
            {
                groupId = g.Id;
                break;
            }
            var members = await groups.ListMembersAsync(g.Id, ct);
            if (members.Any(m => m.UserId == UserId))
            {
                groupId = g.Id;
                break;
            }
        }
        if (groupId is null) return Ok(Array.Empty<GroupOfferListItemDto>());
        return Ok(await _offers.ListForGroupAsync(groupId.Value, ct));
    }

    [HttpPost("expert/group-offers")]
    [Authorize(Roles = UserRoles.Expert)]
    public async Task<ActionResult<GroupOfferListItemDto>> CreateOffer(
        [FromBody] CreateGroupOfferRequest? request,
        [FromServices] IExpertGroupService groups,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var groupId = await ResolveCallerGroupIdAsync(groups, ct)
                ?? throw new InvalidOperationException("Aucun groupe associé.");
            return Ok(await _offers.CreateDraftAsync(groupId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("expert/group-offers/{offerId:guid}/publish")]
    [Authorize(Roles = UserRoles.Expert)]
    public async Task<IActionResult> PublishOffer(Guid offerId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _offers.PublishAsync(offerId, UserId, ct);
            return Ok(new { message = "Offre publiée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/admin-chat")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<GroupAdminConversationDto>>> ManagerConversations(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _chat.ListForManagerAsync(UserId, ct));
    }

    [HttpPost("expert/admin-chat")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<GroupAdminConversationDto>> OpenConversation(
        [FromBody] CreateGroupAdminConversationRequest? request,
        [FromServices] IExpertGroupService groups,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var groupId = await ResolveCallerGroupIdAsync(groups, ct)
                ?? throw new InvalidOperationException("Aucun groupe associé.");
            return Ok(await _chat.OpenOrCreateForGroupAsync(groupId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/admin-chat/{conversationId:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<GroupAdminMessageDto>>> ManagerMessages(Guid conversationId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _chat.ListMessagesAsync(conversationId, ct));
    }

    [HttpPost("expert/admin-chat/{conversationId:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<GroupAdminMessageDto>> ManagerPostMessage(
        Guid conversationId, [FromBody] PostGroupAdminMessageRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await _chat.PostMessageAsync(conversationId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("admin/expert-groups/messages")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<GroupAdminConversationDto>>> AdminConversations(CancellationToken ct)
        => Ok(await _chat.ListForAdminAsync(ct));

    [HttpGet("admin/expert-groups/messages/{conversationId:guid}")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<GroupAdminMessageDto>>> AdminMessages(Guid conversationId, CancellationToken ct)
        => Ok(await _chat.ListMessagesAsync(conversationId, ct));

    [HttpPost("admin/expert-groups/messages/{conversationId:guid}")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<GroupAdminMessageDto>> AdminPostMessage(
        Guid conversationId, [FromBody] PostGroupAdminMessageRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await _chat.PostMessageAsync(conversationId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Guid?> ResolveCallerGroupIdAsync(IExpertGroupService groups, CancellationToken ct)
    {
        if (UserId is null) return null;
        var all = await groups.ListAsync(ct);
        foreach (var g in all)
        {
            if (_managers.IsActiveManager(UserId, g.Id))
                return g.Id;
            var members = await groups.ListMembersAsync(g.Id, ct);
            if (members.Any(m => m.UserId == UserId))
                return g.Id;
        }
        return null;
    }
}
