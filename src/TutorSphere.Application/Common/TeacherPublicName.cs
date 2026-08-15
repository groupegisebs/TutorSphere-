namespace TutorSphere.Application.Common;

/// <summary>
/// Affichage public des enseignants : prénom + initiale du nom, jamais le nom complet.
/// </summary>
public static class TeacherPublicName
{
    public static string Format(string? firstName, string? lastName, string? fallbackGivenName = null)
    {
        var first = NormalizeToken(firstName);
        var last = NormalizeToken(lastName);
        if (first.Length == 0)
            first = FirstToken(fallbackGivenName);

        if (first.Length == 0)
            return last.Length == 0 ? "" : $"{char.ToUpperInvariant(last[0])}.";

        if (last.Length == 0)
            return first;

        return $"{first} {char.ToUpperInvariant(last[0])}.";
    }

    public static string Initials(string? firstName, string? lastName, string? fallbackGivenName = null)
    {
        var first = NormalizeToken(firstName);
        var last = NormalizeToken(lastName);
        if (first.Length == 0)
            first = FirstToken(fallbackGivenName);

        if (first.Length == 0 && last.Length == 0)
            return "?";

        var a = first.Length > 0 ? char.ToUpperInvariant(first[0]) : char.ToUpperInvariant(last[0]);
        if (last.Length == 0 || first.Length == 0)
            return a.ToString();

        return $"{a}{char.ToUpperInvariant(last[0])}";
    }

    /// <summary>Ville et pays uniquement — jamais de rue ni de code postal.</summary>
    public static string? GeneralLocation(string? city, string? country)
    {
        var c = SanitizePlace(city);
        var n = SanitizePlace(country);
        if (c is null && n is null)
            return null;
        if (c is null)
            return n;
        if (n is null)
            return c;
        return $"{c}, {n}";
    }

    public static string FirstToken(string? value)
    {
        var t = (value ?? "").Trim();
        if (t.Length == 0)
            return "";
        var space = t.IndexOf(' ');
        return space < 0 ? t : t[..space].Trim();
    }

    private static string NormalizeToken(string? value)
    {
        var t = (value ?? "").Trim();
        return t.Length == 0 ? "" : t;
    }

    private static string? SanitizePlace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var t = value.Trim();
        if (t.Length == 0)
            return null;
        if (LooksLikeStreetOrPostal(t))
            return null;
        return t;
    }

    private static bool LooksLikeStreetOrPostal(string value) =>
        TeacherContactPrivacy.ContainsResidentialDetails(value);
}
