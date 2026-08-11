using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Document de vérification enseignant (pièce d'identité, diplôme, CV, etc.).</summary>
public class TeacherDocument : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public TeacherDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
