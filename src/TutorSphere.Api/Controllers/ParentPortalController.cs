using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/parent")]
[Authorize(Roles = UserRoles.Parent)]
public class ParentPortalController : ControllerBase
{
    private readonly IParentService _parentService;

    public ParentPortalController(IParentService parentService) => _parentService = parentService;

    [HttpGet("me")]
    public async Task<ActionResult<ParentDto>> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var parent = await _parentService.GetByUserIdAsync(userId, ct);
        return parent is null ? NotFound(new { error = "Profil parent introuvable." }) : Ok(parent);
    }

    [HttpGet("children")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> Children(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _parentService.GetChildrenForUserAsync(userId, ct));
    }

    [HttpPost("children")]
    public async Task<ActionResult<StudentDto>> AddChild([FromBody] ParentAddChildRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var child = await _parentService.AddChildForUserAsync(userId, request, ct);
            return CreatedAtAction(nameof(Children), new { }, child);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
