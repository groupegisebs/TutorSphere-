using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;

    public ParentsController(IParentService parentService) => _parentService = parentService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParentDto>>> List(CancellationToken ct)
        => Ok(await _parentService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParentDto>> GetById(Guid id, CancellationToken ct)
    {
        var parent = await _parentService.GetByIdAsync(id, ct);
        return parent is null ? NotFound() : Ok(parent);
    }

    [HttpPost]
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

    [HttpGet("{id:guid}/children")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetChildren(Guid id, CancellationToken ct)
        => Ok(await _parentService.GetChildrenAsync(id, ct));
}
