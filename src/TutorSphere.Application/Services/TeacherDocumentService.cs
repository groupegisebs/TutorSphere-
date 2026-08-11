using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITeacherDocumentService
{
    Task<IReadOnlyList<TeacherDocumentDto>> ListForOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<TeacherDocumentDto> CreateForOwnerAsync(
        string ownerUserId,
        TeacherDocumentType type,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        string? notes = null,
        CancellationToken ct = default);
    Task DeleteForOwnerAsync(string ownerUserId, Guid documentId, CancellationToken ct = default);
}

public class TeacherDocumentService(
    IApplicationDbContext db,
    IExpertReviewNotificationService expertNotify) : ITeacherDocumentService
{
    public Task<IReadOnlyList<TeacherDocumentDto>> ListForOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var tenant = RequireOwner(ownerUserId);
        IReadOnlyList<TeacherDocumentDto> docs = db.TeacherDocuments
            .Where(d => d.TenantId == tenant.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new TeacherDocumentDto(
                d.Id, d.TenantId, d.DocumentType, d.FileName, d.FileUrl,
                d.ContentType, d.FileSizeBytes, d.CreatedAt, d.Notes))
            .ToList();
        return Task.FromResult(docs);
    }

    public async Task<TeacherDocumentDto> CreateForOwnerAsync(
        string ownerUserId,
        TeacherDocumentType type,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        var tenant = RequireOwner(ownerUserId);
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new InvalidOperationException("URL du fichier requise.");

        var isFirstDocument = !db.TeacherDocuments.Any(d => d.TenantId == tenant.Id);

        var entity = new TeacherDocument
        {
            TenantId = tenant.Id,
            DocumentType = type,
            FileName = fileName.Trim(),
            FileUrl = fileUrl.Trim(),
            ContentType = contentType ?? "application/octet-stream",
            FileSizeBytes = fileSizeBytes,
            UploadedByUserId = uploadedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);

        // Si l'inscription n'a pas encore notifié (ou a échoué), le 1er document déclenche l'alerte.
        if (isFirstDocument)
            await expertNotify.NotifyExpertsIfNeededAsync(tenant.Id, ct);

        return new TeacherDocumentDto(
            entity.Id, entity.TenantId, entity.DocumentType, entity.FileName, entity.FileUrl,
            entity.ContentType, entity.FileSizeBytes, entity.CreatedAt, entity.Notes);
    }

    public async Task DeleteForOwnerAsync(string ownerUserId, Guid documentId, CancellationToken ct = default)
    {
        var tenant = RequireOwner(ownerUserId);
        var doc = db.TeacherDocuments.FirstOrDefault(d => d.Id == documentId && d.TenantId == tenant.Id)
            ?? throw new InvalidOperationException("Document introuvable.");
        db.Remove(doc);
        await db.SaveChangesAsync(ct);
    }

    private Tenant RequireOwner(string ownerUserId) =>
        db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
        ?? throw new InvalidOperationException("Aucun établissement associé à ce compte.");
}
