using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Student : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string? UserId { get; set; }
    public Guid? ParentProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Code de connexion généré par le parent (enfants sans e-mail).
    /// Sert aussi de mot de passe Identity ; renvoyé au parent à la génération uniquement.
    /// </summary>
    public string? LoginAccessCode { get; set; }
    public string? Phone { get; set; }
    public string? SchoolLevel { get; set; }
    public string? SchoolName { get; set; }
    public string? Subjects { get; set; }
    public string? Goals { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DateOfBirth { get; set; }

    // Computed — not stored in the database
    public int? Age => DateOfBirth.HasValue
        ? (int)((DateTime.Today - DateOfBirth.Value).TotalDays / 365.25)
        : null;

    public bool IsMinor => Age.HasValue && Age < 14;
    public bool IsAutonomous => Age.HasValue && Age >= 14;

    public Tenant Tenant { get; set; } = null!;
    public ParentProfile? Parent { get; set; }
    public ICollection<StudentSubscription> Subscriptions { get; set; } = [];
    public ICollection<LessonAttendance> Attendances { get; set; } = [];
    public ICollection<Homework> Homeworks { get; set; } = [];
    public ICollection<LessonReport> Reports { get; set; } = [];
}
