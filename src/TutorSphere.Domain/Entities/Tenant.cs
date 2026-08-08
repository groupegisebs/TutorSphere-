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

    /// <summary>Fin de validité de la licence annuelle plateforme (UTC). Null = jamais payée.</summary>
    public DateTime? LicenseExpiresAt { get; set; }

    /// <summary>Dernier e-mail « renouvellement dans 1 mois » envoyé pour la licence plateforme.</summary>
    public DateTime? LicenseRenewalReminderSentAt { get; set; }

    /// <summary>Date UTC de fin de l'auto-formation enseignant (null = pas encore complétée).</summary>
    public DateTime? OnboardingCompletedAt { get; set; }

    /// <summary>Ids de modules d'auto-formation complétés (séparés par des virgules).</summary>
    public string? OnboardingProgress { get; set; }

    public decimal PlatformCommissionPercent { get; set; } = 10m;
    public string? StripeAccountId { get; set; }
    public string? StripeCustomerId { get; set; }

    /// <summary>E-mail PayPal exigé pour les zones Stripe Connect (et recommandé ailleurs).</summary>
    public string? PayPalEmail { get; set; }

    /// <summary>
    /// Date UTC à partir de laquelle le solde disponible &lt; 100 $ CAD a commencé à être détenu
    /// (délai de 30 jours avant retrait sous le seuil).
    /// </summary>
    public DateTime? PayoutHoldingStartedAt { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public TenantBranding? Branding { get; set; }
    public ICollection<SubscriptionOffering> Offerings { get; set; } = [];
    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Lesson> Lessons { get; set; } = [];
    public ICollection<Unavailability> Unavailabilities { get; set; } = [];
    public ICollection<Holiday> Holidays { get; set; } = [];
    public ICollection<Vacation> Vacations { get; set; } = [];
    public ICollection<PlatformLicensePayment> LicensePayments { get; set; } = [];

    /// <summary>Licence payée et non expirée (formation éventuellement encore requise).</summary>
    public bool HasPaidLicense(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        if (Status is TenantStatus.Rejected or TenantStatus.Suspended)
            return false;
        return LicenseExpiresAt is { } expires && expires > now;
    }

    /// <summary>Établissement pleinement opérationnel et visible (payé + formation terminée).</summary>
    public bool HasValidLicense(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return Status == TenantStatus.Active
               && OnboardingCompletedAt is not null
               && LicenseExpiresAt is { } expires
               && expires > now;
    }

    public bool RequiresOnboarding(DateTime? utcNow = null) =>
        HasPaidLicense(utcNow) && OnboardingCompletedAt is null;
}
