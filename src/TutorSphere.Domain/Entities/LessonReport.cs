using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class LessonReport : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid LessonId { get; set; }
    public Guid StudentId { get; set; }
    public string? TopicsStudied { get; set; }
    public string? Participation { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public string? HomeworkAssigned { get; set; }
    public string? Observations { get; set; }
    public bool SentToParent { get; set; }
    public DateTime? SentAt { get; set; }

    public Lesson Lesson { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
