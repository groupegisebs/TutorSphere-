using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Invoice : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ParentProfileId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CAD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? StripeInvoiceId { get; set; }

    public ParentProfile Parent { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
