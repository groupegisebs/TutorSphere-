using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/library-documents")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertLibraryDocumentsController(
    IDocumentService documents,
    IExpertMonitoringService monitoring,
    IExpertApprovalService approvals,
    IGroupAdminAccessService groupAccess,
    IWebHostEnvironment env) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpertLibraryDocumentDto>>> List(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await documents.ListExpertLibraryAsync(await RequireGroupIdAsync(ct), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Upload(
        IFormFile file,
        [FromForm] string? title,
        [FromForm] string? subject,
        [FromForm] string? schoolLevel,
        [FromForm] string? summary,
        [FromForm] string? teacherTenantIds,
        [FromForm] string? folder,
        CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Fichier requis." });
        if (string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(schoolLevel)
            || string.IsNullOrWhiteSpace(summary))
        {
            return BadRequest(new
            {
                error = "Indiquez le nom du document, la matière, le niveau scolaire et le sommaire."
            });
        }

        var tenantIds = ParseGuidList(teacherTenantIds);
        if (tenantIds.Count == 0)
            return BadRequest(new { error = "Sélectionnez au moins un enseignant." });

        try
        {
            var groupId = await RequireGroupIdAsync(ct);
            var allowed = (await monitoring.ListTeacherDirectoryAsync(
                    UserId, ct, groupAccess.IsPlatformAdmin(User) ? ActAsGroupId : groupId))
                .Select(t => t.TenantId)
                .ToHashSet();
            if (tenantIds.Any(id => !allowed.Contains(id)))
                return BadRequest(new { error = "Un enseignant sélectionné n'appartient pas à votre groupe." });

            var uploadsRoot = UploadsPaths.GetRoot(env);
            var safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsRoot, safeFileName);
            await using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream, ct);

            var created = await documents.CreateForTenantsAsync(
                tenantIds,
                file.FileName,
                file.ContentType,
                file.Length,
                $"/uploads/{safeFileName}",
                UserId,
                string.IsNullOrWhiteSpace(folder) ? "Ressources expert" : folder.Trim(),
                new DocumentWriteRequest(
                    Title: title.Trim(),
                    Subject: subject.Trim(),
                    SchoolLevel: schoolLevel.Trim(),
                    Summary: summary.Trim(),
                    SharedByExpertGroupId: groupId),
                ct);

            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var groupId = await RequireGroupIdAsync(ct);
            var doc = await documents.GetByIdAnyTenantAsync(id, ct);
            if (doc is null || doc.SharedByExpertGroupId != groupId)
                return NotFound();

            var fileName = Path.GetFileName(doc.Url.Replace('\\', '/'));
            var filePath = UploadsPaths.FindExistingFile(env, fileName);
            if (filePath is null)
                return NotFound();

            var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
                ? "application/octet-stream"
                : doc.ContentType;
            return PhysicalFile(filePath, contentType, doc.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("batch/{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await documents.DeleteLibraryBatchAsync(await RequireGroupIdAsync(ct), batchId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Guid> RequireGroupIdAsync(CancellationToken ct)
    {
        var managed = await groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct);
        if (managed is not null)
            return managed.Id;
        if (groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
            return gid;
        if (UserId is null)
            throw new InvalidOperationException("Utilisateur introuvable.");
        var mine = await approvals.GetMyGroupAsync(UserId, ct)
            ?? throw new InvalidOperationException("Accès réservé à un membre actif du groupe d'experts.");
        return mine.Id;
    }

    private static IReadOnlyList<Guid> ParseGuidList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
