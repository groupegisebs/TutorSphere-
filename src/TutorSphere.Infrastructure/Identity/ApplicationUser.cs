using Microsoft.AspNetCore.Identity;

namespace TutorSphere.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string PreferredLanguage { get; set; } = "fr";
    public string TimeZone { get; set; } = "America/Montreal";

    public string FullName => $"{FirstName} {LastName}".Trim();
}
