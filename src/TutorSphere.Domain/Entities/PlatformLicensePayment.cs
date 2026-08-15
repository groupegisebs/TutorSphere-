using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Paiement de la licence annuelle plateforme (activation / renouvellement de la session enseignant).</summary>
public class PlatformLicensePayment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CAD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? GatewayPaymentCode { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
