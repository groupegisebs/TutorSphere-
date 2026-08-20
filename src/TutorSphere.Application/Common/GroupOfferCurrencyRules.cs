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

    /// <summary>Devise de repli quand les pays visés n'ont pas de devise commune.</summary>
    public const string MixedZoneCurrency = "USD";

    /// <summary>
    /// Devise d'une offre valable dans plusieurs pays. Un seul pays garde la devise de ce pays ;
    /// un ensemble homogène garde sa devise commune (toute l'Europe reste en euro) ; dès que les
    /// devises diffèrent — pays de continents différents, ou Canada et États-Unis — l'offre bascule
    /// en USD. Aucun taux de change n'existe dans l'application : une offre affichée dans une devise
    /// est encaissée dans cette devise, il faut donc en choisir une seule.
    /// </summary>
    /// <param name="fallbackCountryCode">Pays du groupe, utilisé quand aucun pays n'est visé.</param>
    public static string ResolveCurrencyForCountries(
        IEnumerable<string?>? countryCodes,
        string? fallbackCountryCode = null)
    {
        var currencies = NormalizeCountryCodes(countryCodes)
            .Select(ResolveCurrency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return currencies.Count switch
        {
            0 => ResolveCurrency(fallbackCountryCode),
            1 => currencies[0],
            _ => MixedZoneCurrency
        };
    }

    /// <summary>Codes ISO normalisés, sans doublon ni vide, dans l'ordre de saisie.</summary>
    public static IReadOnlyList<string> NormalizeCountryCodes(IEnumerable<string?>? countryCodes)
    {
        if (countryCodes is null)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in countryCodes)
        {
            var code = NormalizeCountryCode(raw);
            if (code.Length == 2 && seen.Add(code))
                result.Add(code);
        }
        return result;
    }

    /// <summary>Lit la colonne CSV des pays visés.</summary>
    public static IReadOnlyList<string> ParseCountryCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : NormalizeCountryCodes(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Écrit la colonne CSV des pays visés, <c>null</c> si la liste est vide.</summary>
    public static string? ToCountryCsv(IEnumerable<string?>? countryCodes)
    {
        var codes = NormalizeCountryCodes(countryCodes);
        return codes.Count == 0 ? null : string.Join(',', codes);
    }

    /// <summary>Vrai si tous les pays visés appartiennent à la zone Europe.</summary>
    public static bool AreAllEuropean(IEnumerable<string?>? countryCodes)
    {
        var codes = NormalizeCountryCodes(countryCodes);
        return codes.Count > 0 && codes.All(EuropeanCountries.Contains);
    }
}
