namespace TutorSphere.Domain.Payments;

/// <summary>
/// Pays où Stripe Payments (carte / Checkout) est disponible.
/// Source : liste publique Stripe, mise à jour août 2026.
/// « Réseau étendu » et versions bêta sont inclus : la carte y est proposée.
/// Cameroun, Sénégal, etc. n'y figurent pas — PayPal et Mobile Money à la place.
/// </summary>
public static class StripePaymentsCountries
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "AE", "AT", "AU", "BE", "BG", "BR", "CA", "CH", "CI", "CY", "CZ",
        "DE", "DK", "EE", "ES", "FI", "FR", "GB", "GH", "GI", "GR", "HK",
        "HR", "HU", "ID", "IE", "IN", "IT", "JP", "KE", "LI", "LT", "LU",
        "LV", "MT", "MX", "MY", "NG", "NL", "NO", "NZ", "PL", "PT", "RO",
        "SE", "SG", "SI", "SK", "TH", "UK", "US", "ZA"
    };

    public static bool Contains(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return false;
        var code = countryCode.Trim().ToUpperInvariant();
        if (code is "UK")
            code = "GB";
        return All.Contains(code);
    }
}
