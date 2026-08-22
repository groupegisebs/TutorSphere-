using System.Text.Json;
using System.Text.Json.Nodes;
using TutorSphere.Application.DTOs.Branding;

namespace TutorSphere.Application.Common;

/// <summary>
/// Portefeuille enseignant (expérience, diplômes, certifications, langues) stocké dans
/// <see cref="Domain.Entities.TenantBranding.Portfolio"/>.
/// </summary>
public static class TeacherOnboardingPortfolio
{
    public static string Build(
        string? existingJson,
        IReadOnlyList<string> languageCodes,
        int yearsExperience,
        IEnumerable<PublicCredentialDto>? diplomas,
        IEnumerable<PublicCredentialDto>? certifications)
    {
        var json = TeacherCommunicationLanguages.MergePortfolioLanguages(existingJson, languageCodes);
        JsonObject obj;
        try
        {
            obj = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            obj = new JsonObject();
        }

        obj["yearsExperience"] = Math.Max(0, yearsExperience);
        obj["diplomas"] = ToArray(Clean(diplomas));
        obj["certifications"] = ToArray(Clean(certifications));
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static IReadOnlyList<PublicCredentialDto> Clean(IEnumerable<PublicCredentialDto>? items)
    {
        var list = new List<PublicCredentialDto>();
        foreach (var item in items ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Title))
                continue;
            list.Add(new PublicCredentialDto(
                item.Title.Trim(),
                EmptyToNull(item.Institution),
                EmptyToNull(item.Year)));
        }

        return list;
    }

    private static JsonArray ToArray(IEnumerable<PublicCredentialDto> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            var obj = new JsonObject { ["title"] = item.Title };
            if (!string.IsNullOrWhiteSpace(item.Institution))
                obj["institution"] = item.Institution;
            if (!string.IsNullOrWhiteSpace(item.Year))
                obj["year"] = item.Year;
            arr.Add(obj);
        }

        return arr;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
