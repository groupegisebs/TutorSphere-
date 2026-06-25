using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Student : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string? UserId { get; set; }
    public Guid ParentProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? SchoolLevel { get; set; }
    public string? SchoolName { get; set; }
    public string? Subjects { get; set; }
    public string? Goals { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public ParentProfile Parent { get; set; } = null!;
    public ICollection<StudentSubscription> Subscriptions { get; set; } = [];
    public ICollection<LessonAttendance> Attendances { get; set; } = [];
    public ICollection<Homework> Homeworks { get; set; } = [];
    public ICollection<LessonReport> Reports { get; set; } = [];
}
