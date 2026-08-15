namespace TutorSphere.Application.Common;

public static class ColorHex
{
    public const string TutorSpherePrimary = "#5831E0";
    public const string TutorSphereSecondary = "#4419c8";

    public static string Normalize(string? color, string fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
            return fallback;

        var trimmed = color.Trim();
        if (trimmed.StartsWith('#') && (trimmed.Length == 7 || trimmed.Length == 4)
            && trimmed.Skip(1).All(static c => char.IsAsciiHexDigit(c)))
            return trimmed.Length == 4
                ? $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}"
                : trimmed.ToUpperInvariant();

        return fallback;
    }

    public static string? NormalizeOrNull(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;
        var n = Normalize(color, "");
        return n.Length == 0 ? null : n;
    }
}
