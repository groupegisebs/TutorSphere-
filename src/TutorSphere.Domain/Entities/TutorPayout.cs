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

    /// <summary>Facture générée pour le responsable du groupe (ex. TSG-202608-A1B2C3D4).</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Groupe expert destinataire de la demande de paiement.</summary>
    public Guid? ExpertGroupId { get; set; }

    /// <summary>Copie JSON du moyen de versement au moment de la demande.</summary>
    public string? PaymentMethodSnapshot { get; set; }

    public DateTime? ProcessingAt { get; set; }
    public string? PaidByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public TutorPayoutAccount? PayoutAccount { get; set; }
}
