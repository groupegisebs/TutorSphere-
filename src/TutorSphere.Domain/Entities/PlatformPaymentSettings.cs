using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>
/// Paramètres plateforme du split parent (une seule ligne).
/// Les frais Stripe/PayPal sont déduits du montant payé, puis la commission
/// s’applique sur le net. La licence annuelle enseignant (10 $) reste hors de ce split.
/// </summary>
public class PlatformPaymentSettings : BaseEntity
{
    public decimal DefaultCommissionPercent { get; set; } = 30m;
    public decimal CardFeePercent { get; set; } = 2.9m;
    public decimal CardFeeFixed { get; set; } = 0.30m;
    public decimal PayPalFeePercent { get; set; } = 2.9m;
    public decimal PayPalFeeFixed { get; set; } = 0.30m;
    /// <summary>Frais opérateur MTN/Orange (souvent 1–2,5 %, sans fixe). Réglable en admin.</summary>
    public decimal MobileMoneyFeePercent { get; set; } = 2.0m;
    public decimal MobileMoneyFeeFixed { get; set; } = 0m;
}
