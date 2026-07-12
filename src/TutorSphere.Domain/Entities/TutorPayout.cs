using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Demande d'encaissement des gains du tuteur.
/// Seuls les montants déjà libérés (cours donnés et terminés) peuvent être encaissés.
/// </summary>
public class TutorPayout : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CAD";
    public TutorPayoutStatus Status { get; set; } = TutorPayoutStatus.Pending;
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
