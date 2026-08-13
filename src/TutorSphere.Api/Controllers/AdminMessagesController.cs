using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin/messages")]
[Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class AdminMessagesController : ControllerBase
{
    private readonly IMessageService _messages;
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _config;

    public AdminMessagesController(
        IMessageService messages,
        IEmailService email,
        UserManager<ApplicationUser> users,
        IConfiguration config)
    {
        _messages = messages;
        _email = email;
        _users = users;
        _config = config;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public sealed record AdminSendMessageRequest(
        string RecipientUserId,
        string Subject,
        string Body,
        bool SendEmailCopy = true);

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
            var dto = await _messages.SendAsPlatformAdminAsync(
                UserId,
                new SendMessageRequest(request.RecipientUserId, request.Subject, request.Body),
                ct);

            var emailSent = false;
            string? emailError = null;
            if (request.SendEmailCopy)
            {
                var recipient = await _users.FindByIdAsync(request.RecipientUserId);
                var admin = await _users.FindByIdAsync(UserId);
                if (recipient?.Email is { Length: > 0 } email)
                {
                    try
                    {
                        var webBase = (_config["WebBaseUrl"] ?? "https://tutorsphere.gisebs.com").TrimEnd('/');
                        var inboxPath = await ResolveInboxPathAsync(recipient);
                        await _email.SendAdminDirectMessageAsync(
                            email,
                            recipient.FirstName,
                            admin?.FullName ?? "Administration TutorSphere",
                            request.Subject.Trim(),
                            request.Body.Trim(),
                            $"{webBase}{inboxPath}",
                            ct);
                        emailSent = true;
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }
                else
                {
                    emailError = "Destinataire sans adresse e-mail.";
                }
            }

            return Ok(new
            {
                message = dto,
                emailSent,
                emailError
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<string> ResolveInboxPathAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(UserRoles.Parent)) return "/parent/messages";
        if (roles.Contains(UserRoles.Student)) return "/student/messages";
        if (roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant))
            return "/tutor/messages";
        if (roles.Contains(UserRoles.Expert) || roles.Contains(UserRoles.GroupManager))
            return "/expert/dashboard";
        return "/login";
    }
}
