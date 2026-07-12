using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Application.DTOs.LessonReports;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Application.DTOs.Students;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = UserRoles.Student)]
public class StudentPortalController : ControllerBase
{
    private readonly IStudentPortalService _portal;
    private readonly IWebHostEnvironment _env;

    public StudentPortalController(IStudentPortalService portal, IWebHostEnvironment env)
    {
        _portal = portal;
        _env = env;
    }

    [HttpGet("me")]
    public async Task<ActionResult<StudentDto>> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var student = await _portal.GetMeAsync(userId, ct);
        return student is null ? NotFound(new { error = "Profil élève introuvable." }) : Ok(student);
    }

    [HttpGet("me/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonDto>>> Lessons(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _portal.GetLessonsAsync(userId, start, end, ct));
    }

    [HttpGet("me/homework")]
    public async Task<ActionResult<IReadOnlyList<HomeworkDto>>> Homework(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _portal.GetHomeworkAsync(userId, ct));
    }

    [HttpPost("me/homework/{id:guid}/submit")]
    public async Task<ActionResult<HomeworkDto>> SubmitHomework(
        Guid id,
        [FromBody] SubmitHomeworkRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            return Ok(await _portal.SubmitHomeworkAsync(userId, id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("me/documents")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Documents(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _portal.GetDocumentsAsync(userId, ct));
    }

    [HttpGet("me/documents/{id:guid}/file")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var doc = await _portal.GetDocumentForStudentAsync(userId, id, ct);
        if (doc is null) return NotFound();

        var uploadsRoot = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads");
        var fileName = Path.GetFileName(doc.Url.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            return NotFound();

        var filePath = Path.Combine(uploadsRoot, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
            ? "application/octet-stream"
            : doc.ContentType;
        return PhysicalFile(filePath, contentType, doc.FileName);
    }

    [HttpGet("me/reports")]
    public async Task<ActionResult<IReadOnlyList<LessonReportDto>>> Reports(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _portal.GetReportsAsync(userId, ct));
    }

    /// <summary>Contacts enseignants (école / historique de messages) pour démarrer un chat.</summary>
    [HttpGet("me/teachers")]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> Teachers(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _portal.GetTeacherContactsAsync(userId, ct));
    }
}
