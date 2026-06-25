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

    public Student? Student { get; set; }
    public Lesson? Lesson { get; set; }
}
