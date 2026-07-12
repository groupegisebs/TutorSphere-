using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
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
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });

        try
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsRoot, safeFileName);

            await using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream, ct);

            var fileUrl = $"/uploads/{safeFileName}";
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            var doc = await _documentService.CreateAsync(
                file.FileName,
                file.ContentType,
                file.Length,
                fileUrl,
                userId,
                studentId,
                lessonId,
                folder,
                ct);

            return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var doc = await _documentService.GetByIdAsync(id, ct);
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
