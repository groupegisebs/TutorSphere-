namespace TutorSphere.Domain.Enums;

/// <summary>
/// Canal de versement vers l'enseignant.
/// Stripe Connect : CA / US / UK / EEA / CH.
/// Afrique : Wave + TapTap Send.
/// PayPal : exigé en complément pour les zones Stripe.
/// </summary>
public enum PayoutProviderKind
{
    StripeConnect = 0,
    PayPal = 1,
    Wave = 2,
    TapTapSend = 3
}

public enum PayoutRegionKind
{
    /// <summary>Canada, USA, UK, EEA, Suisse — Stripe Connect + PayPal.</summary>
    StripeConnectZone = 0,
    /// <summary>Afrique — Wave + TapTap Send.</summary>
    Africa = 1,
    Other = 9
}
