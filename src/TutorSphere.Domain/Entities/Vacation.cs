using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class Vacation : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
