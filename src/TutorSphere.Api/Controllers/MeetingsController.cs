using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api;
using TutorSphere.Application.DTOs.Meetings;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/meetings")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class MeetingsController(
    IExpertMeetingService meetings,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);
    private bool AsPlatform => groupAccess.IsPlatformAdmin(User);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeetingListItemDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await meetings.ListAsync(UserId, AsPlatform, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<MeetingGroupOptionDto>>> Groups(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var list = await meetings.ListAccessibleGroupsAsync(UserId, AsPlatform, ActAsGroupId, ct);
            return Ok(list.Select(g => new MeetingGroupOptionDto(g.Id, g.Name, g.Country)).ToList());
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<MeetingCandidatePageDto>> Candidates(
        [FromQuery] string? category,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] MeetingVisibility visibility = MeetingVisibility.CurrentGroup,
        [FromQuery] Guid[]? groupIds = null,
        CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await meetings.SearchCandidatesAsync(
                UserId, category, q, page, pageSize, visibility, groupIds, AsPlatform, ActAsGroupId, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeetingDetailDto>> Get(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await meetings.GetAsync(UserId, id, AsPlatform, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult<MeetingDetailDto>> Create([FromBody] CreateMeetingRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try { return Ok(await meetings.CreateAsync(UserId, request, AsPlatform, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.CancelAsync(UserId, id, AsPlatform, ActAsGroupId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.StartAsync(UserId, id, AsPlatform, ActAsGroupId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.EndForAllAsync(UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/admit/{participantId:guid}")]
    public async Task<IActionResult> Admit(Guid id, Guid participantId, [FromBody] AdmitParticipantRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.AdmitAsync(UserId, id, participantId, body?.Admit ?? true, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/participants/{participantId:guid}/role")]
    public async Task<IActionResult> Role(Guid id, Guid participantId, [FromBody] SetParticipantRoleRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (body is null) return BadRequest(new { error = "Rôle manquant." });
        try
        {
            await meetings.SetParticipantRoleAsync(UserId, id, participantId, body.Role, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/participants/{participantId:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, Guid participantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.RemoveParticipantAsync(UserId, id, participantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, [FromBody] LockMeetingRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.LockAsync(UserId, id, body?.Locked ?? true, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/recording")]
    public async Task<IActionResult> Recording(Guid id, [FromBody] ToggleRecordingRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.ToggleRecordingAsync(UserId, id, body?.Recording ?? true, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/ai/enable")]
    public async Task<IActionResult> EnableAi(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.EnableAiAsync(UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/ai/consent")]
    public async Task<IActionResult> Consent(Guid id, [FromBody] SetAiConsentRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.SetAiConsentAsync(UserId, id, UserId, body?.Consented ?? false, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/ai/generate")]
    public async Task<IActionResult> Generate(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.GenerateAiDraftAsync(UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/minutes")]
    public async Task<ActionResult<MeetingMinutesDto>> Minutes(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await meetings.GetMinutesAsync(UserId, id, AsPlatform, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/minutes/approve")]
    public async Task<IActionResult> ApproveMinutes(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.ApproveMinutesAsync(UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/minutes/share")]
    public async Task<IActionResult> ShareMinutes(Guid id, [FromBody] ShareMinutesRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.SetMinutesShareAsync(UserId, id, body?.Share ?? MeetingMinutesShare.ParticipantsOnly, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/minutes/email")]
    public async Task<IActionResult> EmailMinutes(Guid id, [FromBody] SendMinutesEmailRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.SendMinutesEmailAsync(UserId, id, body?.ExtraEmails, AsPlatform, ActAsGroupId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/actions/{actionId:guid}/review")]
    public async Task<IActionResult> ReviewAction(Guid id, Guid actionId, [FromBody] ReviewActionItemRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (body is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            await meetings.ReviewActionAsync(UserId, id, actionId, body, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/decisions/{decisionId:guid}/review")]
    public async Task<IActionResult> ReviewDecision(Guid id, Guid decisionId, [FromBody] ReviewDecisionRequest? body, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.ReviewDecisionAsync(UserId, id, decisionId, body?.Accepted ?? true, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/guests/{guestId:guid}/revoke")]
    public async Task<IActionResult> RevokeGuest(Guid id, Guid guestId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.RevokeGuestAsync(UserId, id, guestId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/guests/{guestId:guid}/resend")]
    public async Task<IActionResult> ResendGuest(Guid id, Guid guestId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await meetings.ResendGuestAsync(UserId, id, guestId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/calendar.ics")]
    public async Task<IActionResult> Calendar(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var detail = await meetings.GetAsync(UserId, id, AsPlatform, ActAsGroupId, ct);
            var ics = meetings.BuildIcs(detail);
            return File(Encoding.UTF8.GetBytes(ics), "text/calendar", "reunion.ics");
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

[ApiController]
[Route("api/meetings/guest")]
[AllowAnonymous]
public class MeetingGuestController(IExpertMeetingService meetings) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<GuestPreviewDto>> Preview(string token, CancellationToken ct)
    {
        try { return Ok(await meetings.PreviewGuestAsync(token, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("enter")]
    public async Task<ActionResult<GuestEnterResult>> Enter([FromBody] GuestEnterRequest? request, CancellationToken ct)
    {
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try { return Ok(await meetings.EnterGuestAsync(request, ct)); }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("code de vérification", StringComparison.OrdinalIgnoreCase))
                return Accepted(new { error = ex.Message, codeSent = true });
            return BadRequest(new { error = ex.Message });
        }
    }
}
