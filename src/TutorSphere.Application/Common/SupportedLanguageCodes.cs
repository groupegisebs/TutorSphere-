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

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        All.Contains(code, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? code) =>
        IsSupported(code) ? code!.ToLowerInvariant() switch
        {
            "zh-hans" => MandarinChinese,
            _ => code!.Length == 2 ? code.ToLowerInvariant() : code
        } : Default;

    public static IReadOnlyList<CultureInfo> Cultures =>
        All.Select(c => CultureInfo.GetCultureInfo(c)).ToList();
}
