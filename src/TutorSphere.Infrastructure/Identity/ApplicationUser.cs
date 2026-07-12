using Microsoft.AspNetCore.Identity;

namespace TutorSphere.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string PreferredLanguage { get; set; } = "fr";
    public string TimeZone { get; set; } = "America/Montreal";

    /// <summary>When true, receive lesson reminder emails (~24h before).</summary>
    public bool EmailLessonReminders { get; set; } = true;

    /// <summary>Secret token for ICS calendar subscription (webcal / Google / Outlook).</summary>
    public string? CalendarFeedToken { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
