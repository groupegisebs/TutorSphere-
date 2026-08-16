using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Documents;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetAllAsync(Guid? studentId = null, Guid? lessonId = null, CancellationToken ct = default);
    Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto?> GetByIdAnyTenantAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto> CreateAsync(
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        Guid? studentId,
        Guid? lessonId,
        string? folder,
        CancellationToken ct = default,
        DocumentWriteRequest? meta = null);
    Task<IReadOnlyList<DocumentDto>> CreateForTenantsAsync(
        IReadOnlyList<Guid> tenantIds,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        string? folder,
        DocumentWriteRequest meta,
        CancellationToken ct = default);
    Task<IReadOnlyList<ExpertLibraryDocumentDto>> ListExpertLibraryAsync(Guid expertGroupId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteLibraryBatchAsync(Guid expertGroupId, Guid batchId, CancellationToken ct = default);
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
            query = query.Where(d => d.StudentId == studentId.Value || ContainsStudent(d.SharedStudentIds, studentId.Value));
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

    public Task<DocumentDto?> GetByIdAnyTenantAsync(Guid id, CancellationToken ct = default)
    {
        var doc = _db.DocumentsForAnyTenant.FirstOrDefault(d => d.Id == id);
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
        CancellationToken ct = default,
        DocumentWriteRequest? meta = null)
    {
        var tenantId = meta?.TenantId ?? RequireTenantId();
        var doc = BuildDocument(
            tenantId, fileName, contentType, fileSizeBytes, fileUrl, uploadedByUserId,
            studentId, lessonId, folder, meta);
        _db.Add(doc);
        await _db.SaveChangesAsync(ct);
        return MapToDto(doc);
    }

    public async Task<IReadOnlyList<DocumentDto>> CreateForTenantsAsync(
        IReadOnlyList<Guid> tenantIds,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        string? folder,
        DocumentWriteRequest meta,
        CancellationToken ct = default)
    {
        var targets = tenantIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (targets.Count == 0)
            throw new InvalidOperationException("Sélectionnez au moins un enseignant.");

        var existing = _db.Tenants.Where(t => targets.Contains(t.Id)).Select(t => t.Id).ToHashSet();
        if (existing.Count != targets.Count)
            throw new InvalidOperationException("Un enseignant sélectionné est introuvable.");

        var batchId = meta.LibraryBatchId ?? Guid.NewGuid();
        var created = new List<Document>();
        foreach (var tenantId in targets)
        {
            var write = meta with { TenantId = tenantId, LibraryBatchId = batchId };
            var doc = BuildDocument(
                tenantId, fileName, contentType, fileSizeBytes, fileUrl, uploadedByUserId,
                null, null, folder, write);
            _db.Add(doc);
            created.Add(doc);
        }

        await _db.SaveChangesAsync(ct);
        return created.Select(MapToDto).ToList();
    }

    public Task<IReadOnlyList<ExpertLibraryDocumentDto>> ListExpertLibraryAsync(Guid expertGroupId, CancellationToken ct = default)
    {
        var rows = _db.DocumentsForAnyTenant
            .Where(d => d.SharedByExpertGroupId == expertGroupId)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        var tenantIds = rows.Select(d => d.TenantId).Distinct().ToList();
        var names = _db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => t.Name);

        var grouped = rows
            .GroupBy(d => d.LibraryBatchId ?? d.Id)
            .Select(g =>
            {
                var first = g.OrderByDescending(x => x.CreatedAt).First();
                return new ExpertLibraryDocumentDto(
                    g.Key,
                    first.Id,
                    string.IsNullOrWhiteSpace(first.Title) ? first.Name : first.Title!,
                    first.Name,
                    first.Subject,
                    first.SchoolLevel,
                    first.Summary,
                    first.Folder,
                    first.CreatedAt,
                    g.Select(x => names.GetValueOrDefault(x.TenantId, "Enseignant")).Distinct().ToList());
            })
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExpertLibraryDocumentDto>>(grouped);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = _db.Documents.FirstOrDefault(d => d.Id == id)
            ?? throw new InvalidOperationException("Document introuvable.");

        _db.Remove(doc);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteLibraryBatchAsync(Guid expertGroupId, Guid batchId, CancellationToken ct = default)
    {
        var rows = _db.DocumentsForAnyTenant
            .Where(d => d.SharedByExpertGroupId == expertGroupId
                        && (d.LibraryBatchId == batchId || d.Id == batchId))
            .ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException("Document introuvable.");

        _db.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    private static Document BuildDocument(
        Guid tenantId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string fileUrl,
        string uploadedByUserId,
        Guid? studentId,
        Guid? lessonId,
        string? folder,
        DocumentWriteRequest? meta)
    {
        var shared = meta?.SharedStudentIds;
        var primaryStudent = studentId ?? shared?.FirstOrDefault();
        return new Document
        {
            TenantId = tenantId,
            Name = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            FileUrl = fileUrl,
            UploadedByUserId = uploadedByUserId,
            StudentId = primaryStudent is Guid sid && sid != Guid.Empty ? sid : studentId,
            LessonId = lessonId,
            Folder = folder,
            Title = string.IsNullOrWhiteSpace(meta?.Title) ? null : meta!.Title.Trim(),
            Subject = string.IsNullOrWhiteSpace(meta?.Subject) ? null : meta!.Subject.Trim(),
            SchoolLevel = string.IsNullOrWhiteSpace(meta?.SchoolLevel) ? null : meta!.SchoolLevel.Trim(),
            Summary = string.IsNullOrWhiteSpace(meta?.Summary) ? null : meta!.Summary.Trim(),
            SharedStudentIds = JoinIds(shared),
            SharedByExpertGroupId = meta?.SharedByExpertGroupId,
            LibraryBatchId = meta?.LibraryBatchId
        };
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static bool ContainsStudent(string? csv, Guid studentId) =>
        ParseIds(csv).Contains(studentId);

    private static string? JoinIds(IReadOnlyList<Guid>? ids)
    {
        if (ids is null || ids.Count == 0) return null;
        return string.Join(',', ids.Where(id => id != Guid.Empty).Distinct());
    }

    private static IReadOnlyList<Guid> ParseIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private static DocumentDto MapToDto(Document d) => new(
        d.Id,
        d.Name,
        d.FileSizeBytes,
        d.ContentType,
        d.CreatedAt,
        d.StudentId,
        d.LessonId,
        d.FileUrl,
        d.Folder,
        d.Title,
        d.Subject,
        d.SchoolLevel,
        d.Summary,
        ParseIds(d.SharedStudentIds),
        d.SharedByExpertGroupId,
        d.LibraryBatchId);
}
