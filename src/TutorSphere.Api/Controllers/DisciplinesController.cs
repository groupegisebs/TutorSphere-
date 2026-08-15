using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

/// <summary>
/// Gestion, par le groupe d'experts, des disciplines (matières / cycle scolaire), des services fournis
/// et de la méthode de travail associés, ainsi que de l'affectation des enseignants du groupe.
/// </summary>
[ApiController]
[Route("api/expert/disciplines")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class DisciplinesController(
    IExpertDisciplineService disciplines,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DisciplineDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var managed = await groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct);
            return Ok(await disciplines.ListForExpertAsync(UserId, ct, managed?.Id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DisciplineDto>> Get(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await disciplines.GetByIdAsync(id, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<DisciplineDto>> Create([FromBody] CreateDisciplineRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            var created = await disciplines.CreateAsync(UserId, request, ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DisciplineDto>> Update(Guid id, [FromBody] UpdateDisciplineRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (request is null) return BadRequest(new { error = "Requête invalide." });
        try
        {
            return Ok(await disciplines.UpdateAsync(id, UserId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await disciplines.DeleteAsync(id, UserId, ct);
            return Ok(new { message = "Discipline supprimée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/teachers")]
    public async Task<ActionResult<IReadOnlyList<GroupTeacherAssignmentDto>>> Teachers(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await disciplines.ListGroupTeachersAsync(id, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/teachers/{tenantId:guid}")]
    public async Task<IActionResult> AssignTeacher(Guid id, Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await disciplines.AssignTeacherAsync(id, UserId, tenantId, ct);
            return Ok(new { message = "Enseignant affecté." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/teachers/{tenantId:guid}")]
    public async Task<IActionResult> UnassignTeacher(Guid id, Guid tenantId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await disciplines.UnassignTeacherAsync(id, UserId, tenantId, ct);
            return Ok(new { message = "Affectation retirée." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
