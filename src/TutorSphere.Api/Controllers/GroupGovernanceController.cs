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
    private readonly IGroupAdminAccessService _groupAccess;
    private readonly IExpertGroupService _groups;

    private const string ExpertOrManagerOrPlatform =
        $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}";

    private const string ManagerOrPlatform =
        $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}";

    public GroupGovernanceController(
        IGroupOfferService offers,
        IGroupAdminChatService chat,
        ITeacherInterestService interest,
        IExpertGroupManagerService managers,
        IGroupAdminAccessService groupAccess,
        IExpertGroupService groups)
    {
        _offers = offers;
        _chat = chat;
        _interest = interest;
        _managers = managers;
        _groupAccess = groupAccess;
        _groups = groups;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

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
    [Authorize(Roles = ExpertOrManagerOrPlatform)]
    public async Task<ActionResult<GroupOffersCatalogDto>> ListOffers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var groupId = await ResolveCallerGroupIdAsync(ct);
            if (groupId is null)
            {
                return Ok(new GroupOffersCatalogDto(
                    Guid.Empty, "", null, "XAF", false, Array.Empty<GroupOfferListItemDto>()));
            }

            var catalog = await _offers.GetCatalogAsync(groupId.Value, ct);
            return Ok(catalog ?? new GroupOffersCatalogDto(
                groupId.Value, "", null, "XAF", false, Array.Empty<GroupOfferListItemDto>()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("expert/group-offers")]
    [Authorize(Roles = ExpertOrManagerOrPlatform)]
    public async Task<ActionResult<GroupOfferListItemDto>> CreateOffer(
        [FromBody] CreateGroupOfferRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var groupId = await ResolveCallerGroupIdAsync(ct)
                ?? throw new InvalidOperationException(
                    "Aucun groupe associé. Passez en mode « Administrer » depuis le Control Center.");
            return Ok(await _offers.CreateDraftAsync(groupId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("expert/group-offers/{offerId:guid}")]
    [Authorize(Roles = ExpertOrManagerOrPlatform)]
    public async Task<ActionResult<GroupOfferListItemDto>> UpdateOffer(
        Guid offerId,
        [FromBody] UpdateGroupOfferRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            return Ok(await _offers.UpdateDraftAsync(
                offerId, UserId, request, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("expert/group-offers/{offerId:guid}")]
    [Authorize(Roles = ExpertOrManagerOrPlatform)]
    public async Task<IActionResult> DeleteOffer(Guid offerId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _offers.DeleteAsync(offerId, UserId, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
            return Ok(new { message = "Offre supprimée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("expert/group-offers/{offerId:guid}/publish")]
    [Authorize(Roles = ExpertOrManagerOrPlatform)]
    public async Task<IActionResult> PublishOffer(Guid offerId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var asPlatform = _groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
            await _offers.PublishAsync(offerId, UserId, ct, asPlatformAdmin: asPlatform, actAsGroupId: ActAsGroupId);
            return Ok(new { message = "Offre publiée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/admin-chat")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<IReadOnlyList<GroupAdminConversationDto>>> ManagerConversations(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _chat.ListForManagerAsync(UserId, ct));
    }

    [HttpPost("expert/admin-chat")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<GroupAdminConversationDto>> OpenConversation(
        [FromBody] CreateGroupAdminConversationRequest? request,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var groupId = await ResolveCallerGroupIdAsync(ct)
                ?? throw new InvalidOperationException("Aucun groupe associé.");
            return Ok(await _chat.OpenOrCreateForGroupAsync(groupId, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("expert/admin-chat/{conversationId:guid}/messages")]
    [Authorize(Roles = ManagerOrPlatform)]
    public async Task<ActionResult<IReadOnlyList<GroupAdminMessageDto>>> ManagerMessages(Guid conversationId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _chat.ListMessagesAsync(conversationId, ct));
    }

    [HttpPost("expert/admin-chat/{conversationId:guid}/messages")]
    [Authorize(Roles = ManagerOrPlatform)]
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

    /// <summary>
    /// Groupe du Responsable, ou groupe ciblé via header X-Act-As-Expert-Group-Id (SuperAdmin / PlatformAdmin).
    /// </summary>
    private async Task<Guid?> ResolveCallerGroupIdAsync(CancellationToken ct)
    {
        if (UserId is null) return null;

        var managed = await _groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct);
        if (managed is not null)
            return managed.Id;

        // Expert membre (pas Responsable) : premier groupe d'appartenance.
        var all = await _groups.ListAsync(ct);
        foreach (var g in all)
        {
            if (_managers.IsActiveManager(UserId, g.Id))
                return g.Id;
            var members = await _groups.ListMembersAsync(g.Id, ct);
            if (members.Any(m => m.UserId == UserId))
                return g.Id;
        }

        return null;
    }
}
