using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid? studentId = null, Guid? lessonId = null, CancellationToken ct = default);
    Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto> CreateAsync(
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        Guid? studentId,
        Guid? lessonId,
        string? folder,
        CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DocumentService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid? studentId = null, Guid? lessonId = null, CancellationToken ct = default)
    {
        var query = _db.Documents.AsEnumerable();

        if (studentId.HasValue)
            query = query.Where(d => d.StudentId == studentId.Value);
        if (lessonId.HasValue)
            query = query.Where(d => d.LessonId == lessonId.Value);

        var docs = query
            .OrderByDescending(d => d.CreatedAt)
            .Select(MapToDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentDto>>(docs);
    }

    public Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = _db.Documents.FirstOrDefault(d => d.Id == id);
        return Task.FromResult(doc is null ? null : MapToDto(doc));
    }

    public async Task<DocumentDto> CreateAsync(
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        Guid? studentId,
        Guid? lessonId,
        string? folder,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var doc = new Document
        {
            TenantId = tenantId,
            Name = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            FileUrl = fileUrl,
            UploadedByUserId = uploadedByUserId,
            StudentId = studentId,
            LessonId = lessonId,
            Folder = folder
        };

        _db.Add(doc);
        await _db.SaveChangesAsync(ct);
        return MapToDto(doc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = _db.Documents.FirstOrDefault(d => d.Id == id)
            ?? throw new InvalidOperationException("Document introuvable.");

        _db.Remove(doc);
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static DocumentDto MapToDto(Document d) => new(
        d.Id,
        d.Name,
        d.FileSizeBytes,
        d.ContentType,
        d.CreatedAt,
        d.StudentId,
        d.LessonId,
        d.FileUrl);
}
