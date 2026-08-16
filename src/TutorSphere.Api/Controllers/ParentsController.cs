using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api.Filters;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
[RequireActiveTutorLicense]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;

    public ParentsController(IParentService parentService) => _parentService = parentService;

    /// <summary>
    /// Parents dont un enfant est inscrit aux cours de l'enseignant, sans coordonnées :
    /// le contact se fait uniquement par la messagerie interne.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TutorParentDto>>> List(CancellationToken ct)
        => Ok(await _parentService.GetForCurrentTenantAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<ParentDto>> GetById(Guid id, CancellationToken ct)
    {
        var parent = await _parentService.GetByIdAsync(id, ct);
        return parent is null ? NotFound() : Ok(parent);
    }

    // Un enseignant n'ajoute, ne modifie ni ne supprime un parent : le parent crée son compte
    // et inscrit son enfant lui-même. Ces endpoints restent réservés au support plateforme.
    [HttpPost]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<ParentDto>> Create([FromBody] CreateParentRequest request, CancellationToken ct)
    {
        try
        {
            var parent = await _parentService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = parent.Id }, parent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<ActionResult<ParentDto>> Update(Guid id, [FromBody] UpdateParentRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _parentService.UpdateAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _parentService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Enfants de ce parent inscrits aux cours de l'enseignant, uniquement.</summary>
    [HttpGet("{id:guid}/children")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetChildren(Guid id, CancellationToken ct)
        => Ok(await _parentService.GetChildrenForCurrentTenantAsync(id, ct));
}
