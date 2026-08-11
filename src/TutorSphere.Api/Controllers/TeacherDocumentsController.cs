using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/teacher-documents")]
[Authorize(Roles = UserRoles.Tutor)]
public class TeacherDocumentsController : ControllerBase
{
    private readonly ITeacherDocumentService _docs;
    private readonly IExpertApprovalService _approvals;
    private readonly IWebHostEnvironment _env;

    public TeacherDocumentsController(
        ITeacherDocumentService docs,
        IExpertApprovalService approvals,
        IWebHostEnvironment env)
    {
        _docs = docs;
        _approvals = approvals;
        _env = env;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Statut d'approbation expert pour l'enseignant connecté (pas de licence requise).</summary>
    [HttpGet("approval-status")]
    public async Task<ActionResult<TeacherApprovalStatusDto>> ApprovalStatus(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _approvals.GetStatusForOwnerAsync(UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeacherDocumentDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await _docs.ListForOwnerAsync(UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TeacherDocumentDto>> Upload(
        IFormFile file,
        [FromForm] TeacherDocumentType documentType,
        [FromForm] string? notes,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
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
            var doc = await _docs.CreateForOwnerAsync(
                UserId,
                documentType,
                file.FileName,
                file.ContentType,
                file.Length,
                fileUrl,
                UserId,
                notes,
                ct);

            return CreatedAtAction(nameof(List), doc);
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
            await _docs.DeleteForOwnerAsync(UserId, id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
