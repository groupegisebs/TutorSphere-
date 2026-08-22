using System.Text.Json;
using System.Text.Json.Nodes;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Common;

/// <summary>
/// Langues de communication d'un enseignant : plusieurs codes ISO, dont le premier
/// (<see cref="Domain.Entities.Tenant.Language"/>) reste la langue des contrats et de l'interface.
/// </summary>
public static class TeacherCommunicationLanguages
{
    public static IReadOnlyList<string> NormalizeMany(
        IEnumerable<string>? codes,
        string? fallbackPrimary = null)
    {
        var result = new List<string>();
        foreach (var raw in codes ?? [])
        {
            if (!TryParse(raw, out var code))
                continue;
            if (result.All(x => !string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                result.Add(code);
        }

        if (result.Count == 0 && TryParse(fallbackPrimary, out var fallback)
            && result.All(x => !string.Equals(x, fallback, StringComparison.OrdinalIgnoreCase)))
            result.Add(fallback);

        if (result.Count == 0)
            result.Add(SupportedLanguageCodes.Default);

        return result;
    }

    public static bool TryParse(string? raw, out string code)
    {
        code = SupportedLanguageCodes.Default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var token = raw.Trim();
        var lower = token.ToLowerInvariant();
        var mapped = lower switch
        {
            "fr" or "français" or "francais" or "french" => SupportedLanguageCodes.French,
            "en" or "anglais" or "english" => SupportedLanguageCodes.English,
            "es" or "espagnol" or "spanish" or "español" => SupportedLanguageCodes.Spanish,
            "de" or "allemand" or "german" or "deutsch" => SupportedLanguageCodes.German,
            "pt" or "portugais" or "portuguese" or "português" => SupportedLanguageCodes.Portuguese,
            "zh" or "zh-cn" or "zh-hans" or "chinois" or "mandarin" or "chinese" or "中文" or "中文（简体）"
                => SupportedLanguageCodes.MandarinChinese,
            "ar" or "arabe" or "arabic" or "العربية" => SupportedLanguageCodes.Arabic,
            _ => null
        };

        if (mapped is not null)
        {
            code = mapped;
            return true;
        }

        if (!SupportedLanguageCodes.IsSupported(token))
            return false;

        code = SupportedLanguageCodes.Normalize(token);
        return true;
    }

    public static string Primary(IReadOnlyList<string> codes) =>
        codes.Count > 0 ? codes[0] : SupportedLanguageCodes.Default;

    public static string ToCsv(IReadOnlyList<string> codes) =>
        string.Join(",", NormalizeMany(codes));

    public static IReadOnlyList<string> FromCsv(string? csv, string? fallbackPrimary = null)
    {
        var parts = string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return NormalizeMany(parts, fallbackPrimary);
    }

    public static string Label(string? code) => TeacherInviteShareMessage.LanguageLabel(code);

    public static IReadOnlyList<string> PublicLabels(string? csv, string? primary)
    {
        return FromCsv(csv, primary).Select(Label).ToList();
    }

    public static void ApplyToTenant(Tenant tenant, IEnumerable<string>? codes, string? fallbackPrimary = null)
    {
        var list = NormalizeMany(codes, fallbackPrimary ?? tenant.Language);
        tenant.Language = Primary(list);
        tenant.CommunicationLanguagesCsv = ToCsv(list);
    }

    public static void ApplyToBranding(TenantBranding branding, IReadOnlyList<string> codes)
    {
        branding.Portfolio = MergePortfolioLanguages(branding.Portfolio, codes);
        branding.UpdatedAt = DateTime.UtcNow;
    }

    public static string MergePortfolioLanguages(string? existingJson, IReadOnlyList<string> codes)
    {
        JsonObject obj;
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            obj = new JsonObject();
        }
        else
        {
            try
            {
                obj = JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                obj = new JsonObject();
            }
        }

        obj.Remove("Languages");
        var arr = new JsonArray();
        foreach (var label in NormalizeMany(codes).Select(Label))
            arr.Add(label);
        obj["languages"] = arr;
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>Filtre recherche : code primaire ou présence dans le CSV.</summary>
    public static bool CsvContains(string? csv, string? languageCode)
    {
        if (!TryParse(languageCode, out var needle))
            return false;
        var list = FromCsv(csv);
        return list.Any(x => string.Equals(x, needle, StringComparison.OrdinalIgnoreCase));
    }
}
