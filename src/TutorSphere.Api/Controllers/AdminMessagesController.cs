using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Services;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin/messages")]
[Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class AdminMessagesController : ControllerBase
{
    private readonly IMessageService _messages;
    private readonly IAdminMailboxService _mailbox;

    public AdminMessagesController(
        IMessageService messages,
        IAdminMailboxService mailbox)
    {
        _messages = messages;
        _mailbox = mailbox;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public sealed record AdminSendMessageRequest(
        string RecipientUserId,
        string Subject,
        string Body,
        bool SendEmailCopy = true);

    // ── Chat threads (compat) ──

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> Conversations(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _messages.GetAdminConversationsAsync(UserId, ct));
    }

    [HttpGet("conversations/{otherUserId}")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> Thread(string otherUserId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _messages.GetAdminMessagesAsync(UserId, otherUserId, ct));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Send([FromBody] AdminSendMessageRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });

        try
        {
            var result = await _mailbox.ComposeAsync(UserId, new AdminComposeMessageRequest(
                request.RecipientUserId,
                ExternalEmail: null,
                request.Subject,
                request.Body,
                request.SendEmailCopy), ct);

            return Ok(new
            {
                message = new MessageDto(
                    result.Message.Id,
                    result.Message.SenderUserId,
                    result.Message.RecipientUserId,
                    result.Message.Subject,
                    result.Message.Body,
                    result.Message.IsRead,
                    null,
                    result.Message.CreatedAt),
                emailSent = result.EmailSent,
                emailError = result.EmailError
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Mailbox (client mail) ──

    [HttpGet("mailbox/counts")]
    public async Task<ActionResult<MailboxFolderCountsDto>> Counts(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _mailbox.GetCountsAsync(UserId, ct));
    }

    [HttpGet("mailbox")]
    public async Task<ActionResult<IReadOnlyList<MailboxMessageListItemDto>>> List(
        [FromQuery] MailboxFolder folder = MailboxFolder.Inbox,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await _mailbox.ListAsync(UserId, folder, q, ct));
    }

    [HttpGet("mailbox/{id:guid}")]
    public async Task<ActionResult<MailboxMessageDetailDto>> Get(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var msg = await _mailbox.GetAsync(UserId, id, ct);
        return msg is null ? NotFound() : Ok(msg);
    }

    [HttpPost("mailbox/compose")]
    public async Task<ActionResult<AdminMailboxSendResultDto>> Compose(
        [FromBody] AdminComposeMessageRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await _mailbox.ComposeAsync(UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("mailbox/{id:guid}/star")]
    public async Task<ActionResult<MailboxMessageDetailDto>> Star(Guid id, [FromQuery] bool value = true, CancellationToken ct = default)
    {
        if (UserId is null) return Unauthorized();
        var msg = await _mailbox.StarAsync(UserId, id, value, ct);
        return msg is null ? NotFound() : Ok(msg);
    }

    [HttpPost("mailbox/{id:guid}/archive")]
    public async Task<ActionResult<MailboxMessageDetailDto>> Archive(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var msg = await _mailbox.ArchiveAsync(UserId, id, ct);
        return msg is null ? NotFound() : Ok(msg);
    }

    [HttpPost("mailbox/{id:guid}/trash")]
    public async Task<ActionResult<MailboxMessageDetailDto>> Trash(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var msg = await _mailbox.TrashAsync(UserId, id, ct);
        return msg is null ? NotFound() : Ok(msg);
    }

    [HttpDelete("mailbox/{id:guid}")]
    public async Task<IActionResult> DeletePermanent(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var ok = await _mailbox.DeletePermanentAsync(UserId, id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
