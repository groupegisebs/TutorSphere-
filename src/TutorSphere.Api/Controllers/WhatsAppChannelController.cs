using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Settings;

namespace TutorSphere.Api.Controllers;

/// <summary>
/// Canal WhatsApp du compte connecté. Le numéro est toujours celui de la personne authentifiée :
/// aucun endpoint ne permet d'inscrire le numéro de quelqu'un d'autre.
/// </summary>
[ApiController]
[Route("api/me/whatsapp")]
[Authorize]
public class WhatsAppChannelController : ControllerBase
{
    private readonly IWhatsAppEnrollmentService _enrollment;

    public WhatsAppChannelController(IWhatsAppEnrollmentService enrollment) => _enrollment = enrollment;

    [HttpGet]
    public async Task<ActionResult<WhatsAppChannelDto>> Get(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _enrollment.GetAsync(userId, ct));
    }

    [HttpPost("start")]
    public async Task<ActionResult<WhatsAppChannelDto>> Start(
        [FromBody] StartWhatsAppEnrollmentRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _enrollment.StartAsync(userId, request.Phone, ct);
        return Respond(result);
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<WhatsAppChannelDto>> Confirm(
        [FromBody] ConfirmWhatsAppEnrollmentRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _enrollment.ConfirmAsync(userId, request.Code, ct);
        return Respond(result);
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<WhatsAppChannelDto>> UpdatePreferences(
        [FromBody] UpdateWhatsAppPreferencesRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _enrollment.SetPreferencesAsync(userId, request.LessonReminders, ct);
        return Respond(result);
    }

    [HttpDelete]
    public async Task<ActionResult<WhatsAppChannelDto>> OptOut(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _enrollment.OptOutAsync(userId, ct);
        return Respond(result);
    }

    private ActionResult<WhatsAppChannelDto> Respond(WhatsAppChannelResult result)
    {
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(result.Channel);
    }

    private string? CurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return string.IsNullOrEmpty(userId) ? null : userId;
    }
}
