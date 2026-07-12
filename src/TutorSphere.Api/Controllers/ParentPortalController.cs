using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/parent")]
[Authorize(Roles = UserRoles.ParentPortalAccess)]
public class ParentPortalController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IAuthService _authService;
    private readonly IStudentSubscriptionService _subscriptions;

    public ParentPortalController(
        IParentService parentService,
        IAuthService authService,
        IStudentSubscriptionService subscriptions)
    {
        _parentService = parentService;
        _authService = authService;
        _subscriptions = subscriptions;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ParentDto>> Me(CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        var parent = await _parentService.GetByUserIdAsync(userId, ct);
        return parent is null ? NotFound(new { error = "Profil parent introuvable." }) : Ok(parent);
    }

    [HttpGet("children")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> Children(CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        return Ok(await _parentService.GetChildrenForUserAsync(userId, ct));
    }

    [HttpGet("children/{id:guid}")]
    public async Task<ActionResult<StudentDto>> GetChild(Guid id, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        var children = await _parentService.GetChildrenForUserAsync(userId, ct);
        var child = children.FirstOrDefault(c => c.Id == id);
        return child is null ? NotFound(new { error = "Enfant introuvable." }) : Ok(child);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ParentDashboardDto>> Dashboard(CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        var dashboard = await _parentService.GetDashboardForUserAsync(userId, ct);
        return dashboard is null ? NotFound(new { error = "Profil parent introuvable." }) : Ok(dashboard);
    }

    [HttpPost("children")]
    public async Task<ActionResult<StudentDto>> AddChild([FromBody] ParentAddChildRequest request, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        try
        {
            var child = await _parentService.AddChildForUserAsync(userId, request, ct);
            return CreatedAtAction(nameof(GetChild), new { id = child.Id }, child);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Impossible d'enregistrer l'enfant. Vérifiez les informations saisies." });
        }
    }

    [HttpPut("children/{id:guid}")]
    public async Task<ActionResult<StudentDto>> UpdateChild(Guid id, [FromBody] ParentUpdateChildRequest request, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        try
        {
            return Ok(await _parentService.UpdateChildForUserAsync(userId, id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Impossible de mettre à jour l'enfant. Vérifiez les informations saisies." });
        }
    }

    [HttpDelete("children/{id:guid}")]
    public async Task<IActionResult> DeleteChild(Guid id, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        try
        {
            await _parentService.DeleteChildForUserAsync(userId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Impossible de supprimer l'enfant." });
        }
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyList<StudentSubscriptionDto>>> ListSubscriptions(CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        return Ok(await _subscriptions.GetForParentUserAsync(userId, ct));
    }

    [HttpPost("subscriptions/enroll")]
    public async Task<ActionResult<StudentSubscriptionDto>> Enroll([FromBody] EnrollStudentRequest request, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        try
        {
            var sub = await _subscriptions.EnrollAsync(userId, request, ct);
            return Ok(sub);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("subscriptions/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSubscription(Guid id, CancellationToken ct)
    {
        var userId = await ResolveParentUserIdAsync(ct);
        if (userId is null)
            return Unauthorized();

        try
        {
            await _subscriptions.CancelAsync(userId, id, ct);
            return Ok(new { message = "Abonnement annulé." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<string?> ResolveParentUserIdAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return null;

        await _authService.EnsureParentProfileForUserAsync(userId, ct);
        return userId;
    }
}
