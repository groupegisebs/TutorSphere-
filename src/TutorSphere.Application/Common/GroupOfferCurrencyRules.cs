using TutorSphere.Domain.Payouts;

namespace TutorSphere.Application.Common;

/// <summary>
/// Devise d'une offre selon le pays de marché :
/// Europe → EUR · Canada → CAD · USA → USD · Cameroun / Afrique → XAF.
/// </summary>
public static class GroupOfferCurrencyRules
{
    private static readonly HashSet<string> EuropeanCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU",
        "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        "IS", "LI", "NO", "CH", "GB", "UK"
    };

    public static string NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return "";

        var c = countryCode.Trim().ToUpperInvariant();
        return c switch
        {
            "CANADA" => "CA",
            "UNITED STATES" or "USA" or "ÉTATS-UNIS" or "ETATS-UNIS" => "US",
            "UNITED KINGDOM" or "UK" or "ANGLETERRE" or "ROYAUME-UNI" => "GB",
            "SWITZERLAND" or "SUISSE" => "CH",
            "FRANCE" => "FR",
            "CAMEROON" or "CAMEROUN" => "CM",
            "IVORY COAST" or "COTE D'IVOIRE" or "CÔTE D'IVOIRE" => "CI",
            _ => c.Length >= 2 ? c[..2] : c
        };
    }

    public static string ResolveCurrency(string? countryCode)
    {
        var code = NormalizeCountryCode(countryCode);
        if (string.IsNullOrEmpty(code))
            return "XAF";

        if (string.Equals(code, "CA", StringComparison.OrdinalIgnoreCase))
            return "CAD";
        if (string.Equals(code, "US", StringComparison.OrdinalIgnoreCase))
            return "USD";
        if (string.Equals(code, "CM", StringComparison.OrdinalIgnoreCase))
            return "XAF";
        if (EuropeanCountries.Contains(code))
            return "EUR";
        if (TutorPayoutPolicy.AfricaCountries.Contains(code))
            return "XAF";
        return "XAF";
    }

    public static string FormatCurrencyLabel(string currency) => currency.ToUpperInvariant() switch
    {
        "CAD" => "$CAD",
        "USD" => "$USD",
        "EUR" => "EUR",
        "XAF" => "XAF",
        _ => currency.ToUpperInvariant()
    };

    /// <summary>
    /// Pays effectif pour la devise : local = pays du groupe ; international = marché cible.
    /// </summary>
    public static string ResolveOfferCurrency(
        bool isInternational,
        string? marketCountryCode,
        string? groupCountryCode)
    {
        var market = isInternational
            ? NormalizeCountryCode(marketCountryCode)
            : NormalizeCountryCode(groupCountryCode);
        if (string.IsNullOrEmpty(market) && isInternational)
            market = NormalizeCountryCode(marketCountryCode);
        return ResolveCurrency(string.IsNullOrEmpty(market) ? groupCountryCode : market);
    }
}
