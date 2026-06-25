using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Homework : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmissionNotes { get; set; }
    public decimal? Grade { get; set; }
    public string? Feedback { get; set; }
    public bool IsGraded { get; set; }

    public Student Student { get; set; } = null!;
    public Lesson? Lesson { get; set; }
}
