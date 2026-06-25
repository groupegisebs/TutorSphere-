using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.MultiTenancy;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId, string? slug = null)
    {
        TenantId = tenantId;
        TenantSlug = slug;
    }

    public void Clear() => (TenantId, TenantSlug) = (null, null);
}
