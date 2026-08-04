namespace TutorSphere.Domain.Enums;

public enum TenantStatus
{
    PendingValidation = 0,
    Active = 1,
    Suspended = 2,
    Rejected = 3,
    /// <summary>Licence annuelle expirée — renouvellement requis.</summary>
    AwaitingRenewal = 4,
    /// <summary>Licence payée — auto-formation obligatoire avant activation publique.</summary>
    AwaitingOnboarding = 5
}
