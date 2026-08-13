using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Tâche déléguée par le Responsable de groupe à un Expert
/// (profil enseignant, agenda, publication, etc.).
/// </summary>
public class ExpertDelegatedTask : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public Guid TeacherTenantId { get; set; }

    public ExpertDelegatedTaskType TaskType { get; set; }
    public ExpertDelegatedTaskStatus Status { get; set; } = ExpertDelegatedTaskStatus.Open;

    public string CreatedByManagerUserId { get; set; } = string.Empty;
    public string AssigneeExpertUserId { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletionNotes { get; set; }

    public ExpertGroup ExpertGroup { get; set; } = null!;
    public Tenant TeacherTenant { get; set; } = null!;
}
