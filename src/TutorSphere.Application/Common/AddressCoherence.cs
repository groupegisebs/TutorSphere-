using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TutorSphere.Application.Common;

public enum AddressCoherenceKind
{
    PostalFormat,
    PostalOtherCountry,
    CityOtherCountry,
    StreetOtherCountry,
    PhoneOtherCountry
}

public sealed record AddressCoherenceIssue(
    AddressCoherenceKind Kind,
    string CountryIso,
    string? OtherCountryIso = null,
    string? Example = null,
    string? Detail = null);

/// <summary>
/// Empêche une résidence incohérente : code postal, ville, intitulé de rue et numéro
/// doivent coller au pays choisi (ex. pas de G3K 0P6 / Québec avec l’Allemagne).
/// </summary>
public static partial class AddressCoherence
{
    public static AddressCoherenceIssue? Evaluate(
        string? countryIso,
        string? city,
        string? postalCode,
        string? street = null,
        string? phone = null)
    {
        var country = NormalizeIso(countryIso);
        if (country.Length != 2)
            return null;

        var postal = postalCode?.Trim();
        if (!string.IsNullOrWhiteSpace(postal))
        {
            var postalIssue = EvaluatePostal(country, postal);
            if (postalIssue is not null)
                return postalIssue;
        }

        var cityIssue = EvaluateCity(country, city);
        if (cityIssue is not null)
            return cityIssue;

        var streetIssue = EvaluateStreet(country, street);
        if (streetIssue is not null)
            return streetIssue;

        return EvaluatePhone(country, phone);
    }

    public static void ThrowIfInvalid(
        string? countryIso,
        string? city,
        string? postalCode,
        string? street = null,
        string? phone = null)
    {
        var issue = Evaluate(countryIso, city, postalCode, street, phone);
        if (issue is not null)
            throw new InvalidOperationException(FormatMessage(issue));
    }

    public static string FormatMessage(AddressCoherenceIssue issue, Func<string, string>? countryName = null)
    {
        string Name(string iso)
        {
            var n = countryName?.Invoke(iso);
            if (!string.IsNullOrWhiteSpace(n))
                return n;
            return CountryDisplayName(iso);
        }

        var country = Name(issue.CountryIso);
        return issue.Kind switch
        {
            AddressCoherenceKind.PostalFormat =>
                string.IsNullOrWhiteSpace(issue.Example)
                    ? $"Le code postal ne correspond pas au format de {country}."
                    : $"Le code postal ne correspond pas au format de {country}. Exemple : {issue.Example}.",
            AddressCoherenceKind.PostalOtherCountry =>
                $"Ce code postal a le format de {Name(issue.OtherCountryIso ?? "")}, pas de {country}.",
            AddressCoherenceKind.CityOtherCountry =>
                $"« {issue.Detail} » est une ville de {Name(issue.OtherCountryIso ?? "")}. Choisissez ce pays, ou corrigez la ville.",
            AddressCoherenceKind.StreetOtherCountry =>
                $"L’adresse (rue / street / Straße…) ne correspond pas à {country}.",
            AddressCoherenceKind.PhoneOtherCountry =>
                $"Ce numéro de téléphone correspond à {Name(issue.OtherCountryIso ?? "")}, pas à {country}.",
            _ => $"L’adresse ne correspond pas au pays {country}."
        };
    }

    public static string? PostalExample(string? countryIso)
    {
        var country = NormalizeIso(countryIso);
        return Formats.TryGetValue(country, out var spec) ? spec.Example : null;
    }

    public static string? PostalHint(string? countryIso)
    {
        var example = PostalExample(countryIso);
        if (example is null)
            return null;
        var name = CountryDisplayName(NormalizeIso(countryIso));
        return $"Format {name} : {example}";
    }

    private static AddressCoherenceIssue? EvaluatePostal(string country, string postal)
    {
        var compact = CompactPostal(postal);
        var hasOwnFormat = Formats.TryGetValue(country, out var spec);
        if (hasOwnFormat && spec.Pattern.IsMatch(compact))
            return null;

        if (FindDistinctivePostalCountry(postal) is { } other && other != country)
        {
            return new AddressCoherenceIssue(
                AddressCoherenceKind.PostalOtherCountry, country, other,
                hasOwnFormat ? spec.Example : null);
        }

        if (hasOwnFormat)
        {
            return new AddressCoherenceIssue(
                AddressCoherenceKind.PostalFormat, country, Example: spec.Example);
        }

        return null;
    }

    private static AddressCoherenceIssue? EvaluateCity(string country, string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return null;

        var folded = Fold(city);
        if (folded.Length < 3)
            return null;

        foreach (var (name, iso) in Cities)
        {
            if (iso == country)
                continue;
            if (!CityMatches(folded, name))
                continue;
            return new AddressCoherenceIssue(
                AddressCoherenceKind.CityOtherCountry, country, iso, Detail: city.Trim());
        }

        return null;
    }

    private static AddressCoherenceIssue? EvaluateStreet(string country, string? street)
    {
        if (string.IsNullOrWhiteSpace(street))
            return null;

        var folded = Fold(street);
        if (folded.Contains("strasse", StringComparison.Ordinal)
            || folded.Contains("straße", StringComparison.Ordinal))
        {
            if (!Germanic.Contains(country))
                return new AddressCoherenceIssue(AddressCoherenceKind.StreetOtherCountry, country, "DE");
            return null;
        }

        if (HasAnyToken(folded, "rue", "chemin", "impasse", "rang", "boul", "boulevard"))
        {
            if (!Francophone.Contains(country))
                return new AddressCoherenceIssue(AddressCoherenceKind.StreetOtherCountry, country, "FR");
            return null;
        }

        if (HasAnyToken(folded, "street", "road", "lane", "drive", "avenue"))
        {
            if (Anglophone.Contains(country) || Francophone.Contains(country))
                return null;
            if (Germanic.Contains(country) || Latin.Contains(country))
                return new AddressCoherenceIssue(AddressCoherenceKind.StreetOtherCountry, country, "GB");
        }

        if (HasAnyToken(folded, "calle", "avenida") && !Iberian.Contains(country))
            return new AddressCoherenceIssue(AddressCoherenceKind.StreetOtherCountry, country, "ES");

        return null;
    }

    private static AddressCoherenceIssue? EvaluatePhone(string country, string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
            return null;

        if (DialByIso.TryGetValue(country, out var ownDial)
            && digits.StartsWith(ownDial, StringComparison.Ordinal)
            && digits.Length > ownDial.Length)
        {
            digits = digits[ownDial.Length..];
        }

        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        if (digits.Length == 10 && CanadianAreaCodes.Contains(digits[..3]))
        {
            if (country is "CA")
                return null;
            return new AddressCoherenceIssue(AddressCoherenceKind.PhoneOtherCountry, country, "CA");
        }

        return null;
    }

    private static string? FindDistinctivePostalCountry(string postal)
    {
        var compact = CompactPostal(postal);
        foreach (var (iso, spec) in DistinctiveFormats)
        {
            if (spec.Pattern.IsMatch(compact))
                return iso;
        }

        return null;
    }

    private static string CompactPostal(string postal) =>
        Regex.Replace(postal.Trim().ToUpperInvariant(), @"\s+", " ");

    private static bool CityMatches(string foldedCity, string foldedName) =>
        foldedCity == foldedName
        || foldedCity.StartsWith(foldedName + " ", StringComparison.Ordinal)
        || foldedCity.StartsWith(foldedName + ",", StringComparison.Ordinal)
        || foldedCity.EndsWith(" " + foldedName, StringComparison.Ordinal);

    private static bool HasAnyToken(string folded, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (TokenRegex(token).IsMatch(folded))
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex ExtraSpaces();

    private static Regex TokenRegex(string token) =>
        new($@"\b{Regex.Escape(token)}\b", RegexOptions.CultureInvariant);

    private static string NormalizeIso(string? iso) =>
        string.IsNullOrWhiteSpace(iso) ? "" : iso.Trim().ToUpperInvariant();

    private static string Fold(string value)
    {
        var n = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return ExtraSpaces().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    private static string CountryDisplayName(string iso) =>
        DisplayNames.TryGetValue(iso, out var name) ? name : iso;

    private readonly record struct PostalSpec(Regex Pattern, string Example);

    private static readonly IReadOnlyDictionary<string, PostalSpec> Formats =
        new Dictionary<string, PostalSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["CA"] = new(CaPostal(), "G3K 0P6"),
            ["US"] = new(UsPostal(), "10001"),
            ["DE"] = new(FiveDigits(), "10115"),
            ["FR"] = new(FiveDigits(), "75001"),
            ["IT"] = new(FiveDigits(), "00100"),
            ["ES"] = new(FiveDigits(), "28001"),
            ["MA"] = new(FiveDigits(), "20000"),
            ["SN"] = new(FiveDigits(), "12500"),
            ["BE"] = new(FourDigits(), "1000"),
            ["CH"] = new(FourDigits(), "1200"),
            ["AT"] = new(FourDigits(), "1010"),
            ["AU"] = new(FourDigits(), "2000"),
            ["ZA"] = new(FourDigits(), "8001"),
            ["GB"] = new(GbPostal(), "SW1A 1AA"),
            ["NL"] = new(NlPostal(), "1012 JS"),
            ["PT"] = new(PtPostal(), "1000-001"),
            ["BR"] = new(BrPostal(), "01310-100"),
            ["JP"] = new(JpPostal(), "100-0001"),
            ["NG"] = new(SixDigits(), "100001"),
            ["IN"] = new(SixDigits(), "110001"),
            ["PL"] = new(PlPostal(), "00-001"),
        };

    /// <summary>Formats that cannot be confused with a plain 4/5-digit postal code.</summary>
    private static readonly (string Iso, PostalSpec Spec)[] DistinctiveFormats =
    [
        ("CA", Formats["CA"]),
        ("GB", Formats["GB"]),
        ("NL", Formats["NL"]),
        ("PT", Formats["PT"]),
        ("BR", Formats["BR"]),
        ("JP", Formats["JP"]),
        ("PL", Formats["PL"]),
    ];

    [GeneratedRegex(@"^[A-Z]\d[A-Z][ -]?\d[A-Z]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex CaPostal();

    [GeneratedRegex(@"^\d{5}(-\d{4})?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsPostal();

    [GeneratedRegex(@"^\d{5}$", RegexOptions.CultureInvariant)]
    private static partial Regex FiveDigits();

    [GeneratedRegex(@"^\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex FourDigits();

    [GeneratedRegex(@"^\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex SixDigits();

    [GeneratedRegex(@"^[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex GbPostal();

    [GeneratedRegex(@"^\d{4}\s?[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex NlPostal();

    [GeneratedRegex(@"^\d{4}-?\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PtPostal();

    [GeneratedRegex(@"^\d{5}-?\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex BrPostal();

    [GeneratedRegex(@"^\d{3}-?\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex JpPostal();

    [GeneratedRegex(@"^\d{2}-?\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PlPostal();

    private static readonly HashSet<string> Francophone = new(StringComparer.OrdinalIgnoreCase)
    {
        "FR", "BE", "CH", "LU", "MC", "CA", "HT", "SN", "CI", "CM", "CD", "CG", "GA", "BJ", "BF",
        "ML", "NE", "TG", "GN", "GW", "TD", "CF", "MG", "BI", "RW", "DJ", "KM", "SC",
        "GF", "GP", "MQ", "RE", "YT", "NC", "PF", "PM", "WF"
    };

    private static readonly HashSet<string> Germanic = new(StringComparer.OrdinalIgnoreCase)
    {
        "DE", "AT", "CH", "LI"
    };

    private static readonly HashSet<string> Anglophone = new(StringComparer.OrdinalIgnoreCase)
    {
        "GB", "US", "CA", "AU", "NZ", "IE", "ZA", "NG", "GH", "KE", "UG", "ZW", "JM", "TT", "IN", "SG"
    };

    private static readonly HashSet<string> Latin = new(StringComparer.OrdinalIgnoreCase)
    {
        "IT", "ES", "PT", "PL", "RO", "NL"
    };

    private static readonly HashSet<string> Iberian = new(StringComparer.OrdinalIgnoreCase)
    {
        "ES", "MX", "AR", "CL", "CO", "PE", "VE", "EC", "UY", "PY", "BO", "CR", "PA", "GT", "HN",
        "SV", "NI", "DO", "CU", "PR"
    };

    private static readonly IReadOnlyDictionary<string, string> DialByIso =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CA"] = "1", ["US"] = "1", ["FR"] = "33", ["BE"] = "32", ["CH"] = "41",
            ["GB"] = "44", ["DE"] = "49", ["ES"] = "34", ["IT"] = "39", ["PT"] = "351",
            ["NL"] = "31", ["AT"] = "43", ["PL"] = "48", ["CI"] = "225", ["CM"] = "237",
            ["SN"] = "221", ["MA"] = "212", ["ZA"] = "27"
        };

    private static readonly HashSet<string> CanadianAreaCodes = new(StringComparer.Ordinal)
    {
        "204", "226", "236", "249", "250", "257", "263", "289", "306", "343", "354", "365",
        "367", "368", "382", "387", "403", "416", "418", "428", "431", "437", "438", "450",
        "468", "474", "506", "514", "519", "548", "579", "581", "584", "587", "604", "613",
        "639", "647", "672", "683", "705", "709", "742", "753", "778", "780", "782", "807",
        "819", "825", "867", "873", "879", "902", "905", "942"
    };

    /// <summary>Villes dont le nom identifie un pays (pas de homonymes fréquents : Paris, London…).</summary>
    private static readonly (string Name, string Iso)[] Cities =
    [
        ("quebec", "CA"), ("montreal", "CA"), ("toronto", "CA"), ("vancouver", "CA"),
        ("ottawa", "CA"), ("calgary", "CA"), ("edmonton", "CA"), ("winnipeg", "CA"),
        ("halifax", "CA"), ("gatineau", "CA"), ("sherbrooke", "CA"),
        ("longueuil", "CA"), ("saguenay", "CA"), ("trois-rivieres", "CA"), ("levis", "CA"),
        ("mississauga", "CA"), ("brampton", "CA"), ("kitchener", "CA"),
        ("new york", "US"), ("los angeles", "US"), ("chicago", "US"), ("houston", "US"),
        ("miami", "US"), ("boston", "US"), ("seattle", "US"), ("san francisco", "US"),
        ("washington", "US"), ("atlanta", "US"), ("dallas", "US"), ("philadelphia", "US"),
        ("berlin", "DE"), ("munich", "DE"), ("munchen", "DE"), ("hamburg", "DE"),
        ("frankfurt", "DE"), ("stuttgart", "DE"), ("dusseldorf", "DE"), ("koln", "DE"),
        ("cologne", "DE"), ("dortmund", "DE"), ("leipzig", "DE"), ("dresden", "DE"),
        ("lyon", "FR"), ("marseille", "FR"), ("toulouse", "FR"), ("lille", "FR"),
        ("bordeaux", "FR"), ("nantes", "FR"), ("strasbourg", "FR"), ("nice", "FR"),
        ("rennes", "FR"), ("montpellier", "FR"),
        ("abidjan", "CI"), ("yamoussoukro", "CI"), ("bouake", "CI"),
        ("douala", "CM"), ("yaounde", "CM"), ("garoua", "CM"),
        ("dakar", "SN"), ("thies", "SN"),
        ("lagos", "NG"), ("abuja", "NG"),
        ("johannesburg", "ZA"), ("cape town", "ZA"), ("le cap", "ZA"), ("durban", "ZA"), ("pretoria", "ZA"),
        ("casablanca", "MA"), ("rabat", "MA"), ("marrakech", "MA"),
        ("bruxelles", "BE"), ("brussels", "BE"), ("anvers", "BE"), ("liege", "BE"),
        ("geneve", "CH"), ("zurich", "CH"), ("lausanne", "CH"), ("berne", "CH"),
        ("madrid", "ES"), ("barcelone", "ES"), ("valencia", "ES"),
        ("rome", "IT"), ("milan", "IT"), ("naples", "IT"), ("turin", "IT"),
        ("lisbonne", "PT"), ("lisbon", "PT"), ("porto", "PT"),
        ("amsterdam", "NL"), ("rotterdam", "NL"), ("la haye", "NL"),
        ("londres", "GB"), ("manchester", "GB"), ("birmingham", "GB"), ("liverpool", "GB"),
        ("glasgow", "GB"), ("edimbourg", "GB"), ("edinburgh", "GB"),
        ("tokyo", "JP"), ("osaka", "JP"),
        ("sydney", "AU"), ("melbourne", "AU"), ("brisbane", "AU"),
        ("accra", "GH"), ("nairobi", "KE"), ("kampala", "UG"),
        ("libreville", "GA")
    ];

    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CA"] = "Canada", ["US"] = "États-Unis", ["DE"] = "Allemagne", ["FR"] = "France",
            ["GB"] = "Royaume-Uni", ["BE"] = "Belgique", ["CH"] = "Suisse", ["IT"] = "Italie",
            ["ES"] = "Espagne", ["PT"] = "Portugal", ["NL"] = "Pays-Bas", ["AT"] = "Autriche",
            ["CI"] = "Côte d'Ivoire", ["CM"] = "Cameroun", ["SN"] = "Sénégal", ["MA"] = "Maroc",
            ["ZA"] = "Afrique du Sud", ["NG"] = "Nigeria", ["AU"] = "Australie", ["JP"] = "Japon",
            ["PL"] = "Pologne", ["BR"] = "Brésil", ["IN"] = "Inde"
        };
}
