using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class SubscriptionOffering : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "CAD";
    public int DurationDays { get; set; }
    public int SessionCount { get; set; }
    public string? Frequency { get; set; }
    public int MaxCapacity { get; set; } = 20;
    public LessonMode Mode { get; set; } = LessonMode.Online;
    public string? Conditions { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<StudentSubscription> Subscriptions { get; set; } = [];
}
