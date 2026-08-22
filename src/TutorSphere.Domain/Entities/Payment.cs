using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Payment : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public decimal ProcessorFee { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal TutorAmount { get; set; }
    public decimal GroupAmount { get; set; }
    public Guid? ExpertGroupId { get; set; }
    public decimal CommissionPercent { get; set; }
    public string Currency { get; set; } = "CAD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? StripePaymentIntentId { get; set; }
    /// <summary>ORANGE | MTN (collecte Mobile Money) — null pour Stripe.</summary>
    public string? Channel { get; set; }
    /// <summary>Numéro masqué uniquement.</summary>
    public string? PhoneMasked { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Invoice? Invoice { get; set; }
    public StudentSubscription? Subscription { get; set; }
}
