using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Coordonnées de versement de l'enseignant.
/// Jamais de PIN, mot de passe ou identifiants de connexion bancaire.
/// </summary>
public class TutorPayoutAccount : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Label { get; set; } = string.Empty;
    public PayoutProviderKind ProviderKind { get; set; }
    public string CountryCode { get; set; } = "CA";
    public string Currency { get; set; } = "CAD";
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Nom du titulaire (compte / portefeuille).</summary>
    public string AccountHolderName { get; set; } = string.Empty;

    /// <summary>E-mail PayPal ou identifiant Stripe Connect (acct_…).</summary>
    public string? EmailOrAccountId { get; set; }

    /// <summary>Téléphone Wave / TapTap Send (E.164 de préférence).</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Détails libres nécessaires au versement (banque, opérateur, notes) — pas de secrets.</summary>
    public string? PaymentDetails { get; set; }

    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
