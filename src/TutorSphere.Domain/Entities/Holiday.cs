using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Holiday : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
