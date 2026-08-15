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

    /// <summary>
    /// Codes ISO des pays où la fiche peut être vue (CSV, ex. "CM,SN,CI").
    /// Null/vide = uniquement le pays de l'enseignant (<see cref="Country"/>).
    /// </summary>
    public string? VisibleCountryCodes { get; set; }

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

    /// <summary>
    /// Solde restant de la retenue à la source (équivalent 10 $ USD) à prélever
    /// sur les premiers paiements dus à l'enseignant. 0 = rien à retenir.
    /// </summary>
    public decimal LicenseFeeWithholdingRemainingUsd { get; set; }

    /// <summary>pay | promo | withhold — dernier mode d'activation de session.</summary>
    public string? LicenseSettlementKind { get; set; }

    /// <summary>
    /// Renouvellement annuel automatique par retenue à la source (10 $ USD)
    /// sur les paiements dus à l'enseignant, notamment après un code promo.
    /// </summary>
    public bool LicenseAutoRenewAtSource { get; set; }

    /// <summary>Dernier e-mail « renouvellement dans 1 mois » envoyé pour la licence plateforme.</summary>
    public DateTime? LicenseRenewalReminderSentAt { get; set; }

    /// <summary>Date UTC de fin de l'auto-formation enseignant (null = pas encore complétée).</summary>
    public DateTime? OnboardingCompletedAt { get; set; }

    /// <summary>Ids de modules d'auto-formation complétés (séparés par des virgules).</summary>
    public string? OnboardingProgress { get; set; }

    /// <summary>Version du code de conduite enseignant acceptée (ex. 2026.08).</summary>
    public string? TeacherConductPolicyVersion { get; set; }

    /// <summary>Date UTC d'acceptation du code de conduite enseignant.</summary>
    public DateTime? TeacherConductAcceptedAt { get; set; }

    /// <summary>Validation par un groupe d'experts (indépendante licence / onboarding).</summary>
    public ExpertApprovalStatus ExpertApprovalStatus { get; set; } = ExpertApprovalStatus.Pending;

    /// <summary>Groupe d'experts ayant approuvé (ou rejeté) la fiche.</summary>
    public Guid? ApprovedByExpertGroupId { get; set; }

    /// <summary>Utilisateur expert ayant pris la décision.</summary>
    public string? ApprovedByUserId { get; set; }

    public DateTime? ExpertApprovedAt { get; set; }

    /// <summary>Commentaire optionnel de l'expert (approbation ou rejet).</summary>
    public string? ExpertApprovalNotes { get; set; }

    /// <summary>
    /// Dernier e-mail « fiche en attente » envoyé aux experts du groupe responsable (anti-spam).
    /// </summary>
    public DateTime? ExpertReviewNotifiedAt { get; set; }

    /// <summary>Expert actuellement responsable du dossier (revue).</summary>
    public string? ReviewAssignedToUserId { get; set; }

    /// <summary>0 = normal, 1 = urgent.</summary>
    public int ReviewPriority { get; set; }

    /// <summary>Dernière demande de modifications / notes de revue en cours.</summary>
    public string? ReviewRequestNotes { get; set; }

    public DateTime? ReviewAssignedAt { get; set; }

    public ExpertGroup? ApprovedByExpertGroup { get; set; }

    public ICollection<TeacherDocument> TeacherDocuments { get; set; } = [];

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
    public ICollection<TeacherAvailability> Availabilities { get; set; } = [];
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

    /// <summary>Session enseignant pleinement opérationnelle et visible (licence + formation terminée).</summary>
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
