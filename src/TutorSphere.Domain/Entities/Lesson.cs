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
    public DateTime? ReminderSentAt { get; set; }

    /// <summary>Statut de comptabilisation (absence / annulation / moniteur).</summary>
    public LessonSettlementStatus SettlementStatus { get; set; } = LessonSettlementStatus.Scheduled;

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>True si une séance a été déduite du forfait élève.</summary>
    public bool SessionCounted { get; set; }

    /// <summary>True si le moniteur doit replanifier ou rembourser (TutorNoShow).</summary>
    public bool TutorLiable { get; set; }

    /// <summary>reschedule | refund</summary>
    public string? TutorLiabilityResolution { get; set; }
    public DateTime? TutorLiabilityResolvedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<LessonAttendance> Attendances { get; set; } = [];
    public ICollection<LessonReport> Reports { get; set; } = [];
    public ICollection<Homework> Homeworks { get; set; } = [];
}
