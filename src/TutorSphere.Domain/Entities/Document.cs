using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Document : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Folder { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? LessonId { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;

    /// <summary>Nom affiché du document (distinct du nom de fichier).</summary>
    public string? Title { get; set; }
    public string? Subject { get; set; }
    public string? SchoolLevel { get; set; }
    public string? Summary { get; set; }
    /// <summary>Identifiants d'élèves destinataires, séparés par des virgules.</summary>
    public string? SharedStudentIds { get; set; }
    public Guid? SharedByExpertGroupId { get; set; }
    public Guid? LibraryBatchId { get; set; }

    public Student? Student { get; set; }
    public Lesson? Lesson { get; set; }
}
