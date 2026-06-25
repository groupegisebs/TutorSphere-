using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Parent},{UserRoles.Student},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService) => _messageService = messageService;

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> GetConversations(CancellationToken ct) =>
        Ok(await _messageService.GetConversationsAsync(GetUserId(), ct));

    [HttpGet("conversations/{otherUserId}")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetConversationMessages(
        string otherUserId,
        CancellationToken ct) =>
        Ok(await _messageService.GetMessagesAsync(GetUserId(), otherUserId, ct));

    [HttpPost]
    public async Task<ActionResult<MessageDto>> Send([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        try
        {
            var message = await _messageService.SendAsync(GetUserId(), request, ct);
            return CreatedAtAction(nameof(GetConversationMessages), new { otherUserId = request.RecipientUserId }, message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{messageId:guid}/read")]
    public async Task<ActionResult<MessageDto>> MarkAsRead(Guid messageId, CancellationToken ct)
    {
        var message = await _messageService.MarkAsReadAsync(GetUserId(), messageId, ct);
        return message is null ? NotFound() : Ok(message);
    }

    [HttpPut("conversations/{otherUserId}/read")]
    public async Task<IActionResult> MarkConversationAsRead(string otherUserId, CancellationToken ct)
    {
        await _messageService.MarkConversationAsReadAsync(GetUserId(), otherUserId, ct);
        return NoContent();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Utilisateur non authentifié.");
}
