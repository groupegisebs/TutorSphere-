using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class LessonAttendance : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid LessonId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsPresent { get; set; }
    public string? Notes { get; set; }

    public Lesson Lesson { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
