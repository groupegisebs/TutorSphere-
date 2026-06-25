using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Lesson : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public LessonMode Mode { get; set; } = LessonMode.Online;
    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }
    public string? SessionNotes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<LessonAttendance> Attendances { get; set; } = [];
    public ICollection<LessonReport> Reports { get; set; } = [];
    public ICollection<Homework> Homeworks { get; set; } = [];
}
