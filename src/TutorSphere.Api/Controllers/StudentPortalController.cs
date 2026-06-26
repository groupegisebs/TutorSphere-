using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = UserRoles.Student)]
public class StudentPortalController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentPortalController(IStudentService studentService) => _studentService = studentService;

    [HttpGet("me")]
    public async Task<ActionResult<StudentDto>> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var student = await _studentService.GetByUserIdAsync(userId, ct);
        return student is null ? NotFound(new { error = "Profil élève introuvable." }) : Ok(student);
    }
}
