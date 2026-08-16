using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api;
using TutorSphere.Api.Filters;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
[RequireActiveTutorLicense]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IWebHostEnvironment _env;

    public DocumentsController(IDocumentService documentService, IWebHostEnvironment env)
    {
        _documentService = documentService;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? lessonId,
        CancellationToken ct)
        => Ok(await _documentService.GetAllAsync(studentId, lessonId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
    {
        var doc = await _documentService.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload(
        IFormFile file,
        [FromForm] Guid? studentId,
        [FromForm] Guid? lessonId,
        [FromForm] string? folder,
        [FromForm] string? title,
        [FromForm] string? subject,
        [FromForm] string? schoolLevel,
        [FromForm] string? summary,
        [FromForm] string? sharedStudentIds,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });

        var studentIds = ParseGuidList(sharedStudentIds);
        var hasMeta = HasAnyMeta(title, subject, schoolLevel, summary) || studentIds.Count > 0;
        if (hasMeta && !HasCompleteMeta(title, subject, schoolLevel, summary))
            return BadRequest(new
            {
                error = "Indiquez le nom du document, la matière, le niveau scolaire et le sommaire."
            });

        try
        {
            var uploadsRoot = UploadsPaths.GetRoot(_env);

            var safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsRoot, safeFileName);

            await using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream, ct);

            var fileUrl = $"/uploads/{safeFileName}";
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var meta = hasMeta
                ? new DocumentWriteRequest(
                    Title: title?.Trim(),
                    Subject: subject?.Trim(),
                    SchoolLevel: schoolLevel?.Trim(),
                    Summary: summary?.Trim(),
                    SharedStudentIds: studentIds)
                : null;

            var doc = await _documentService.CreateAsync(
                file.FileName,
                file.ContentType,
                file.Length,
                fileUrl,
                userId,
                studentId,
                lessonId,
                folder,
                ct,
                meta);

            return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool HasAnyMeta(params string?[] values) =>
        values.Any(v => !string.IsNullOrWhiteSpace(v));

    private static bool HasCompleteMeta(string? title, string? subject, string? schoolLevel, string? summary) =>
        !string.IsNullOrWhiteSpace(title)
        && !string.IsNullOrWhiteSpace(subject)
        && !string.IsNullOrWhiteSpace(schoolLevel)
        && !string.IsNullOrWhiteSpace(summary);

    private static IReadOnlyList<Guid> ParseGuidList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var doc = await _documentService.GetByIdAsync(id, ct);
        if (doc is null) return NotFound();

        var fileName = Path.GetFileName(doc.Url.Replace('\\', '/'));
        var filePath = UploadsPaths.FindExistingFile(_env, fileName);
        if (filePath is null)
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
            ? "application/octet-stream"
            : doc.ContentType;
        return PhysicalFile(filePath, contentType, doc.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _documentService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
