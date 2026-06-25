using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class TenantBranding : BaseEntity
{
    public Guid TenantId { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string PrimaryColor { get; set; } = "#2563eb";
    public string SecondaryColor { get; set; } = "#1e40af";
    public string? Presentation { get; set; }
    public string? Portfolio { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
