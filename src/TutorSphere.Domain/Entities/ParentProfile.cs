using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public class ParentProfile : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    /// <summary>Code ISO 3166-1 alpha-2 du pays du parent / élève (visibilité des fiches enseignants).</summary>
    public string? Country { get; set; }
    public string? StripeCustomerId { get; set; }

    /// <summary>Code de parrainage unique (ex. FAM-AB12CD).</summary>
    public string? ReferralCode { get; set; }

    /// <summary>Parent qui a parrainé ce compte (null si inscription organique).</summary>
    public Guid? ReferredByParentProfileId { get; set; }

    /// <summary>Mois gratuits accumulés via parrainage (1 mois par filleul inscrit).</summary>
    public int ReferralRewardMonths { get; set; }

    public ParentProfile? ReferredByParent { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Student> Children { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<ParentSupportRequest> SupportRequests { get; set; } = [];
}
