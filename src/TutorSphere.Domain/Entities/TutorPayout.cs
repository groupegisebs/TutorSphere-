using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Demande d'encaissement des gains du tuteur.
/// Seuls les montants déjà libérés (cours donnés et terminés) peuvent être encaissés.
/// Le paiement réel passe par la file PayGateway (rapprochement admin).
/// </summary>
public class TutorPayout : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? PayoutAccountId { get; set; }
    public PayoutProviderKind? ProviderKind { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CAD";
    public TutorPayoutStatus Status { get; set; } = TutorPayoutStatus.Pending;
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Clé d'idempotence envoyée à PayGateway.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Id de la demande dans PayGateway (SellerDisbursementRequest).</summary>
    public string? ExternalDisbursementId { get; set; }

    /// <summary>Id provider final (transfer / payout batch / mm_ready_…).</summary>
    public string? ProviderPayoutId { get; set; }

    public string? FailureMessage { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public TutorPayoutAccount? PayoutAccount { get; set; }
}
