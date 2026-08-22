using System.Globalization;

namespace TutorSphere.Application.Common;

/// <summary>
/// Supported UI and tenant language codes for TutorSphere.
/// Use these values for <see cref="Domain.Entities.Tenant.Language"/>
/// and <see cref="Infrastructure.Identity.ApplicationUser.PreferredLanguage"/>.
/// </summary>
public static class SupportedLanguageCodes
{
    public const string French = "fr";
    public const string English = "en";
    public const string Spanish = "es";
    public const string German = "de";
    public const string Portuguese = "pt";
    public const string MandarinChinese = "zh-Hans";
    public const string Arabic = "ar";

    public static readonly string Default = French;

    public static readonly string[] All =
    [
        French,
        English,
        Spanish,
        German,
        Portuguese,
        MandarinChinese,
        Arabic
    ];

    public static bool IsSupported(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        if (All.Contains(code, StringComparer.OrdinalIgnoreCase))
            return true;
        var n = code.Trim().ToLowerInvariant();
        return n is "zh" or "zh-cn";
    }

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Default;
        var trimmed = code.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower is "zh" or "zh-cn" or "zh-hans")
            return MandarinChinese;
        if (!IsSupported(trimmed))
            return Default;
        return trimmed.Length == 2 ? lower : trimmed;
    }

    public static IList<CultureInfo> Cultures =>
        All.Select(c => CultureInfo.GetCultureInfo(c)).ToList();

    public static CultureInfo GetCulture(string? code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(Normalize(code));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(French);
        }
    }
}
