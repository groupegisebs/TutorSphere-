using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Payouts;

/// <summary>
/// Règles de retrait TutorSphere (montants en CAD net).
/// ≥ 100 $ CAD → réclamable immédiatement.
/// &lt; 100 $ CAD → délai de 30 jours.
/// &lt; 10 $ CAD → aucun transfert (y compris fin de mois).
/// </summary>
public static class TutorPayoutPolicy
{
    public const decimal InstantClaimThresholdCad = 100m;
    public const decimal MinimumTransferCad = 10m;
    public const int HoldingDaysUnderThreshold = 30;
    public const string PolicyCurrency = "CAD";

    /// <summary>Pays éligibles Stripe Connect (cross-border self-serve).</summary>
    public static readonly HashSet<string> StripeConnectCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        // Canada / US / UK / CH
        "CA", "US", "GB", "UK", "CH",
        // EEA
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU",
        "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        "IS", "LI", "NO"
    };

    public static readonly HashSet<string> AfricaCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "DZ", "AO", "BJ", "BW", "BF", "BI", "CM", "CV", "CF", "TD", "KM", "CG", "CD", "CI",
        "DJ", "EG", "GQ", "ER", "SZ", "ET", "GA", "GM", "GH", "GN", "GW", "KE", "LS", "LR",
        "LY", "MG", "MW", "ML", "MR", "MU", "MA", "MZ", "NA", "NE", "NG", "RW", "ST", "SN",
        "SC", "SL", "SO", "ZA", "SS", "SD", "TZ", "TG", "TN", "UG", "ZM", "ZW"
    };

    public static string NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return "CA";

        var c = country.Trim().ToUpperInvariant();
        // Noms fréquents → ISO
        return c switch
        {
            "CANADA" => "CA",
            "UNITED STATES" or "USA" or "ÉTATS-UNIS" or "ETATS-UNIS" => "US",
            "UNITED KINGDOM" or "UK" or "ANGLETERRE" or "ROYAUME-UNI" => "GB",
            "SWITZERLAND" or "SUISSE" => "CH",
            "FRANCE" => "FR",
            "CAMEROON" or "CAMEROUN" => "CM",
            "IVORY COAST" or "COTE D'IVOIRE" or "CÔTE D'IVOIRE" => "CI",
            "SENEGAL" or "SÉNÉGAL" => "SN",
            _ => c.Length == 2 ? c : c
        };
    }

    public static PayoutRegionKind ResolveRegion(string? country)
    {
        var code = NormalizeCountry(country);
        if (StripeConnectCountries.Contains(code))
            return PayoutRegionKind.StripeConnectZone;
        if (AfricaCountries.Contains(code))
            return PayoutRegionKind.Africa;
        return PayoutRegionKind.Other;
    }

    public static IReadOnlyList<PayoutProviderKind> RequiredProviders(PayoutRegionKind region) =>
        region switch
        {
            PayoutRegionKind.StripeConnectZone => [PayoutProviderKind.StripeConnect, PayoutProviderKind.PayPal],
            PayoutRegionKind.Africa => AfricaMobileMoneyProviders,
            _ => [PayoutProviderKind.PayPal]
        };

    public static IReadOnlyList<PayoutProviderKind> AfricaMobileMoneyProviders { get; } =
    [
        PayoutProviderKind.Wave,
        PayoutProviderKind.OrangeMoney,
        PayoutProviderKind.MtnMomo,
        PayoutProviderKind.TapTapSend,
        PayoutProviderKind.Mpesa,
        PayoutProviderKind.Moov,
        PayoutProviderKind.Airtel
    ];

    public static bool RequiresPayPalAtSignup(string? country) =>
        ResolveRegion(country) is PayoutRegionKind.StripeConnectZone or PayoutRegionKind.Other;

    public static bool RequiresStripeAtSignup(string? country) => false;
}
