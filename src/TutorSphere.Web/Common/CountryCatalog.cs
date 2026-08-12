namespace TutorSphere.Web.Common;

/// <summary>ISO 3166-1 alpha-2 country code + French display name.</summary>
public sealed record CountryInfo(string Code, string Name)
{
    /// <summary>Unicode regional-indicator flag emoji, derived from the ISO code (no image assets needed).</summary>
    public string Flag => CountryCatalog.FlagEmoji(Code);
}

/// <summary>Static catalog of countries used for country pickers (expert groups, tenants, payouts…).</summary>
public static class CountryCatalog
{
    public static readonly IReadOnlyList<CountryInfo> All = BuildAll();

    private static readonly Dictionary<string, CountryInfo> ByCode =
        All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public static CountryInfo? Find(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.TryGetValue(code.Trim(), out var c) ? c : null;

    public static List<CountryInfo> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All.ToList();
        var q = query.Trim();
        return All
            .Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Code.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Converts a 2-letter ISO code (e.g. "CA") into its regional-indicator flag emoji (e.g. 🇨🇦).</summary>
    public static string FlagEmoji(string? iso2)
    {
        if (string.IsNullOrWhiteSpace(iso2) || iso2.Trim().Length != 2)
            return "🏳️";

        var code = iso2.Trim().ToUpperInvariant();
        if (code[0] is < 'A' or > 'Z' || code[1] is < 'A' or > 'Z')
            return "🏳️";

        return string.Concat(
            char.ConvertFromUtf32(0x1F1E6 + (code[0] - 'A')),
            char.ConvertFromUtf32(0x1F1E6 + (code[1] - 'A')));
    }

    private static List<CountryInfo> BuildAll()
    {
        var raw = new (string Code, string Name)[]
        {
            ("AF", "Afghanistan"), ("ZA", "Afrique du Sud"), ("AL", "Albanie"), ("DE", "Allemagne"),
            ("AD", "Andorre"), ("AO", "Angola"), ("AI", "Anguilla"), ("AQ", "Antarctique"),
            ("AG", "Antigua-et-Barbuda"), ("SA", "Arabie saoudite"), ("AR", "Argentine"), ("AM", "Arménie"),
            ("AW", "Aruba"), ("AU", "Australie"), ("AT", "Autriche"), ("AZ", "Azerbaïdjan"),
            ("BS", "Bahamas"), ("BH", "Bahreïn"), ("BD", "Bangladesh"), ("BB", "Barbade"),
            ("BE", "Belgique"), ("BZ", "Belize"), ("BJ", "Bénin"), ("BM", "Bermudes"),
            ("BT", "Bhoutan"), ("BY", "Biélorussie"), ("BO", "Bolivie"), ("BA", "Bosnie-Herzégovine"),
            ("BW", "Botswana"), ("BR", "Brésil"), ("BN", "Brunei"), ("BG", "Bulgarie"),
            ("BF", "Burkina Faso"), ("BI", "Burundi"), ("KH", "Cambodge"), ("CM", "Cameroun"),
            ("CA", "Canada"), ("CV", "Cap-Vert"), ("CL", "Chili"), ("CN", "Chine"),
            ("CY", "Chypre"), ("CO", "Colombie"), ("KM", "Comores"), ("CG", "Congo-Brazzaville"),
            ("CD", "Congo-Kinshasa (RDC)"), ("KR", "Corée du Sud"), ("KP", "Corée du Nord"), ("CR", "Costa Rica"),
            ("CI", "Côte d'Ivoire"), ("HR", "Croatie"), ("CU", "Cuba"), ("DK", "Danemark"),
            ("DJ", "Djibouti"), ("DO", "République dominicaine"), ("DM", "Dominique"), ("EG", "Égypte"),
            ("SV", "Salvador"), ("AE", "Émirats arabes unis"), ("EC", "Équateur"), ("ER", "Érythrée"),
            ("ES", "Espagne"), ("EE", "Estonie"), ("SZ", "Eswatini"), ("US", "États-Unis"),
            ("ET", "Éthiopie"), ("FJ", "Fidji"), ("FI", "Finlande"), ("FR", "France"),
            ("GA", "Gabon"), ("GM", "Gambie"), ("GE", "Géorgie"), ("GH", "Ghana"),
            ("GI", "Gibraltar"), ("GR", "Grèce"), ("GD", "Grenade"), ("GL", "Groenland"),
            ("GP", "Guadeloupe"), ("GU", "Guam"), ("GT", "Guatemala"), ("GG", "Guernesey"),
            ("GN", "Guinée"), ("GQ", "Guinée équatoriale"), ("GW", "Guinée-Bissau"), ("GY", "Guyana"),
            ("GF", "Guyane française"), ("HT", "Haïti"), ("HN", "Honduras"), ("HK", "Hong Kong"),
            ("HU", "Hongrie"), ("IN", "Inde"), ("ID", "Indonésie"), ("IQ", "Irak"),
            ("IR", "Iran"), ("IE", "Irlande"), ("IS", "Islande"), ("IL", "Israël"),
            ("IT", "Italie"), ("JM", "Jamaïque"), ("JP", "Japon"), ("JE", "Jersey"),
            ("JO", "Jordanie"), ("KZ", "Kazakhstan"), ("KE", "Kenya"), ("KG", "Kirghizistan"),
            ("KI", "Kiribati"), ("KW", "Koweït"), ("LA", "Laos"), ("LS", "Lesotho"),
            ("LV", "Lettonie"), ("LB", "Liban"), ("LR", "Liberia"), ("LY", "Libye"),
            ("LI", "Liechtenstein"), ("LT", "Lituanie"), ("LU", "Luxembourg"), ("MO", "Macao"),
            ("MK", "Macédoine du Nord"), ("MG", "Madagascar"), ("MY", "Malaisie"), ("MW", "Malawi"),
            ("MV", "Maldives"), ("ML", "Mali"), ("MT", "Malte"), ("MA", "Maroc"),
            ("MQ", "Martinique"), ("MU", "Maurice"), ("MR", "Mauritanie"), ("YT", "Mayotte"),
            ("MX", "Mexique"), ("MD", "Moldavie"), ("MC", "Monaco"), ("MN", "Mongolie"),
            ("ME", "Monténégro"), ("MS", "Montserrat"), ("MZ", "Mozambique"), ("MM", "Myanmar"),
            ("NA", "Namibie"), ("NR", "Nauru"), ("NP", "Népal"), ("NI", "Nicaragua"),
            ("NE", "Niger"), ("NG", "Nigeria"), ("NU", "Niue"), ("NO", "Norvège"),
            ("NC", "Nouvelle-Calédonie"), ("NZ", "Nouvelle-Zélande"), ("OM", "Oman"), ("UG", "Ouganda"),
            ("UZ", "Ouzbékistan"), ("PK", "Pakistan"), ("PW", "Palaos"), ("PS", "Palestine"),
            ("PA", "Panama"), ("PG", "Papouasie-Nouvelle-Guinée"), ("PY", "Paraguay"), ("NL", "Pays-Bas"),
            ("PE", "Pérou"), ("PH", "Philippines"), ("PL", "Pologne"), ("PF", "Polynésie française"),
            ("PR", "Porto Rico"), ("PT", "Portugal"), ("QA", "Qatar"), ("RE", "Réunion"),
            ("RO", "Roumanie"), ("GB", "Royaume-Uni"), ("RU", "Russie"), ("RW", "Rwanda"),
            ("KN", "Saint-Kitts-et-Nevis"), ("SM", "Saint-Marin"), ("PM", "Saint-Pierre-et-Miquelon"),
            ("VC", "Saint-Vincent-et-les-Grenadines"), ("LC", "Sainte-Lucie"),
            ("SB", "Îles Salomon"), ("WS", "Samoa"), ("ST", "Sao Tomé-et-Principe"), ("SN", "Sénégal"),
            ("RS", "Serbie"), ("SC", "Seychelles"), ("SL", "Sierra Leone"), ("SG", "Singapour"),
            ("SK", "Slovaquie"), ("SI", "Slovénie"), ("SO", "Somalie"), ("SD", "Soudan"),
            ("SS", "Soudan du Sud"), ("LK", "Sri Lanka"), ("SE", "Suède"), ("CH", "Suisse"),
            ("SR", "Suriname"), ("SY", "Syrie"), ("TJ", "Tadjikistan"), ("TW", "Taïwan"),
            ("TZ", "Tanzanie"), ("TD", "Tchad"), ("CZ", "Tchéquie"), ("TH", "Thaïlande"),
            ("TL", "Timor oriental"), ("TG", "Togo"), ("TO", "Tonga"), ("TT", "Trinité-et-Tobago"),
            ("TN", "Tunisie"), ("TM", "Turkménistan"), ("TR", "Turquie"), ("TV", "Tuvalu"),
            ("UA", "Ukraine"), ("UY", "Uruguay"), ("VU", "Vanuatu"), ("VA", "Vatican"),
            ("VE", "Venezuela"), ("VN", "Vietnam"), ("YE", "Yémen"), ("ZM", "Zambie"),
            ("ZW", "Zimbabwe")
        };

        return raw
            .Select(r => new CountryInfo(r.Code, r.Name))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
