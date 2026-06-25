using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class StudentSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid OfferingId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SessionsRemaining { get; set; }
    public string? StripeSubscriptionId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public SubscriptionOffering Offering { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
