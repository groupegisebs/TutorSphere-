using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Proposition de remplaçant pour une séance : un membre du groupe d'experts
/// affecte les heures d'un enseignant indisponible. Le parent (ou l'élève autonome)
/// doit approuver. Si approuvé, le suppléant donne le cours et perçoit la part tuteur.
/// </summary>
public class LessonCoverageAssignment : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public Guid OriginalTenantId { get; set; }
    public Guid SubstituteTenantId { get; set; }
    public Guid LessonId { get; set; }
    public Guid? UnavailabilityId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string ProposedByUserId { get; set; } = string.Empty;

    public LessonCoverageStatus Status { get; set; } = LessonCoverageStatus.Pending;
    public DateTime? RespondedAt { get; set; }
    public string? RespondedByUserId { get; set; }

    public DateTime? TransferredAt { get; set; }
    public decimal? TransferredTutorAmount { get; set; }
    public string TransferCurrency { get; set; } = "CAD";
}
