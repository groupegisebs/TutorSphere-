using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Subdomain { get; set; }
    public string? Description { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string TimeZone { get; set; } = "America/Montreal";
    public string Currency { get; set; } = "CAD";

    /// <summary>
    /// Primary language for this tenant. Supported codes: fr, en, es, de, pt, zh-Hans, ar.
    /// </summary>
    public string Language { get; set; } = "fr";
    public TenantPlan Plan { get; set; } = TenantPlan.Starter;
    public TenantStatus Status { get; set; } = TenantStatus.PendingValidation;
    public bool IsPublicProfile { get; set; }
    public decimal PlatformCommissionPercent { get; set; } = 10m;
    public string? StripeAccountId { get; set; }
    public string? StripeCustomerId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;

    public TenantBranding? Branding { get; set; }
    public ICollection<SubscriptionOffering> Offerings { get; set; } = [];
    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Lesson> Lessons { get; set; } = [];
    public ICollection<Unavailability> Unavailabilities { get; set; } = [];
    public ICollection<Holiday> Holidays { get; set; } = [];
    public ICollection<Vacation> Vacations { get; set; } = [];
}
