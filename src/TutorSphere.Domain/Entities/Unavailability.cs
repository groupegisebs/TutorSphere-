using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Unavailability : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string? Reason { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
