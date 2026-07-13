namespace TutorSphere.Domain.Enums;

/// <summary>
/// Canal de versement vers l'enseignant.
/// Stripe Connect : CA / US / UK / EEA / CH.
/// Afrique : Mobile Money (Wave, Orange Money, MTN, M-Pesa, etc.) — infos publiques uniquement.
/// PayPal : exigé en complément pour les zones Stripe.
/// </summary>
public enum PayoutProviderKind
{
    StripeConnect = 0,
    PayPal = 1,
    Wave = 2,
    TapTapSend = 3,
    OrangeMoney = 4,
    MtnMomo = 5,
    Mpesa = 6,
    Moov = 7,
    Airtel = 8
}

public enum PayoutRegionKind
{
    /// <summary>Canada, USA, UK, EEA, Suisse — Stripe Connect + PayPal.</summary>
    StripeConnectZone = 0,
    /// <summary>Afrique — Mobile Money (infos publiques).</summary>
    Africa = 1,
    Other = 9
}

public static class PayoutProviderCodes
{
    public static string ToPayGatewayCode(PayoutProviderKind kind) => kind switch
    {
        PayoutProviderKind.StripeConnect => "stripe_connect",
        PayoutProviderKind.PayPal => "paypal",
        PayoutProviderKind.Wave => "wave",
        PayoutProviderKind.TapTapSend => "taptapsend",
        PayoutProviderKind.OrangeMoney => "orange_money",
        PayoutProviderKind.MtnMomo => "mtn_momo",
        PayoutProviderKind.Mpesa => "mpesa",
        PayoutProviderKind.Moov => "moov",
        PayoutProviderKind.Airtel => "airtel",
        _ => kind.ToString().ToLowerInvariant()
    };

    public static bool IsMobileMoney(PayoutProviderKind kind) =>
        kind is PayoutProviderKind.Wave
            or PayoutProviderKind.TapTapSend
            or PayoutProviderKind.OrangeMoney
            or PayoutProviderKind.MtnMomo
            or PayoutProviderKind.Mpesa
            or PayoutProviderKind.Moov
            or PayoutProviderKind.Airtel;
}
