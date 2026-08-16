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

    public static readonly HashSet<string> WestAfricaFrancCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "BJ", "BF", "CI", "GW", "ML", "NE", "SN", "TG"
    };

    /// <summary>Devise de versement selon le pays du titulaire.</summary>
    public static string ResolvePayoutCurrency(string? country)
    {
        var code = NormalizeCountry(country);
        if (string.Equals(code, "CA", StringComparison.OrdinalIgnoreCase))
            return "CAD";
        if (string.Equals(code, "US", StringComparison.OrdinalIgnoreCase))
            return "USD";
        if (string.Equals(code, "GB", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "UK", StringComparison.OrdinalIgnoreCase))
            return "GBP";
        if (string.Equals(code, "CH", StringComparison.OrdinalIgnoreCase))
            return "CHF";
        if (StripeConnectCountries.Contains(code))
            return "EUR";
        if (WestAfricaFrancCountries.Contains(code))
            return "XOF";
        if (AfricaCountries.Contains(code))
            return "XAF";
        return "XAF";
    }

    public static string FormatPayoutCurrencyLabel(string? currency) => (currency ?? "").ToUpperInvariant() switch
    {
        "XOF" => "XOF (FCFA)",
        "XAF" => "XAF (FCFA)",
        "CAD" => "CAD ($)",
        "USD" => "USD ($)",
        "EUR" => "EUR (€)",
        "GBP" => "GBP (£)",
        "CHF" => "CHF",
        _ => string.IsNullOrWhiteSpace(currency) ? "—" : currency.ToUpperInvariant()
    };

    public static bool RequiresPayPalAtSignup(string? country) =>
        ResolveRegion(country) is PayoutRegionKind.StripeConnectZone or PayoutRegionKind.Other;

    public static bool RequiresStripeAtSignup(string? country) => false;

    /// <summary>Interac e-Transfer (virement bancaire) disponible uniquement au Canada.</summary>
    public static bool SupportsInteracETransfer(string? country) =>
        string.Equals(NormalizeCountry(country), "CA", StringComparison.OrdinalIgnoreCase);
}
