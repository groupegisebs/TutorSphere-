using TutorSphere.Application.Common;
using TutorSphere.Domain.Entities;

namespace TutorSphere.UnitTests;

public class TeacherCommunicationLanguagesTests
{
    [Fact]
    public void NormalizeMany_keeps_order_and_primary()
    {
        var codes = TeacherCommunicationLanguages.NormalizeMany(["en", "fr", "ar", "en"]);
        Assert.Equal(["en", "fr", "ar"], codes);
        Assert.Equal("en", TeacherCommunicationLanguages.Primary(codes));
        Assert.Equal("en,fr,ar", TeacherCommunicationLanguages.ToCsv(codes));
    }

    [Fact]
    public void NormalizeMany_maps_zh_and_english_labels()
    {
        var codes = TeacherCommunicationLanguages.NormalizeMany(["zh", "English", "Français"]);
        Assert.Equal(
            [SupportedLanguageCodes.MandarinChinese, SupportedLanguageCodes.English, SupportedLanguageCodes.French],
            codes);
    }

    [Fact]
    public void NormalizeMany_empty_falls_back_to_french()
    {
        var codes = TeacherCommunicationLanguages.NormalizeMany(null);
        Assert.Equal([SupportedLanguageCodes.French], codes);
    }

    [Fact]
    public void FromCsv_roundtrip()
    {
        var parsed = TeacherCommunicationLanguages.FromCsv("pt, de , zh-Hans");
        Assert.Equal(["pt", "de", "zh-Hans"], parsed);
        Assert.Equal("pt,de,zh-Hans", TeacherCommunicationLanguages.ToCsv(parsed));
    }

    [Fact]
    public void ApplyToTenant_sets_primary_and_csv()
    {
        var tenant = new Tenant { Language = "fr" };
        TeacherCommunicationLanguages.ApplyToTenant(tenant, ["es", "en"], "fr");
        Assert.Equal("es", tenant.Language);
        Assert.Equal("es,en", tenant.CommunicationLanguagesCsv);
    }

    [Fact]
    public void MergePortfolioLanguages_writes_labels_and_keeps_other_keys()
    {
        var json = TeacherCommunicationLanguages.MergePortfolioLanguages(
            """{"yearsExperience":4,"Languages":["legacy"]}""",
            ["fr", "en"]);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("yearsExperience").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("Languages", out _));
        var langs = doc.RootElement.GetProperty("languages").EnumerateArray()
            .Select(x => x.GetString())
            .ToList();
        Assert.Equal(["Français", "English"], langs);
    }

    [Fact]
    public void SupportedLanguageCodes_normalize_zh_alias()
    {
        Assert.Equal(SupportedLanguageCodes.MandarinChinese, SupportedLanguageCodes.Normalize("zh"));
        Assert.Equal(SupportedLanguageCodes.MandarinChinese, SupportedLanguageCodes.Normalize("zh-CN"));
    }

    [Fact]
    public void CsvContains_matches_secondary_language()
    {
        Assert.True(TeacherCommunicationLanguages.CsvContains("fr,en,ar", "en"));
        Assert.False(TeacherCommunicationLanguages.CsvContains("fr,ar", "en"));
    }
}
