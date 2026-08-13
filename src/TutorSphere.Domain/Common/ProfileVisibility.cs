namespace TutorSphere.Domain.Common;

/// <summary>
/// Visibilité géographique d'une fiche enseignant.
/// Par défaut : uniquement le pays de l'enseignant (ISO 3166-1 alpha-2).
/// </summary>
public static class ProfileVisibility
{
    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";
        var t = code.Trim().ToUpperInvariant();
        return t.Length >= 2 ? t[..2] : t;
    }

    public static IReadOnlyList<string> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCode)
            .Where(c => c.Length == 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>CSV normalisé ; si vide, utilise le pays d'origine.</summary>
    public static string ToCsv(IEnumerable<string>? codes, string? homeCountry)
    {
        var list = Parse(string.Join(",", codes ?? []));
        if (list.Count == 0)
        {
            var home = NormalizeCode(homeCountry);
            if (home.Length == 2)
                list = [home];
        }

        return string.Join(",", list);
    }

    public static bool IsVisibleTo(string? visibleCountryCodesCsv, string? homeCountry, string? viewerCountry)
    {
        var viewer = NormalizeCode(viewerCountry);
        if (viewer.Length != 2)
            return true; // pas de pays spectateur → pas de filtre (ex. fiche directe)

        var visible = Parse(visibleCountryCodesCsv);
        if (visible.Count == 0)
        {
            var home = NormalizeCode(homeCountry);
            // Pays enseignant non renseigné : ne pas masquer la fiche publique.
            if (home.Length != 2)
                return true;
            return home.Equals(viewer, StringComparison.OrdinalIgnoreCase);
        }

        return visible.Any(c => c.Equals(viewer, StringComparison.OrdinalIgnoreCase));
    }
}
