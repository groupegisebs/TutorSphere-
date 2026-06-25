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
    public string? StripeCustomerId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Student> Children { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
}
