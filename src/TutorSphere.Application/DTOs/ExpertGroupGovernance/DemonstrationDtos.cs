using System.Text.Json;
using System.Text.Json.Serialization;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertGroupGovernance;

public sealed class DemonstrationPayload
{
    public string? Subject { get; set; }
    public string? Level { get; set; }
    public string? Topic { get; set; }
    public int DurationMinutes { get; set; } = 45;
    public string? Location { get; set; }
    public List<string> EvaluatorUserIds { get; set; } = [];
    /// <summary>1 Démarrage, 2 Présentation, 3 Évaluation, 4 Compte rendu, 5 Décision finale.</summary>
    public int Step { get; set; } = 1;
    public DateTime? SessionOpenedAtUtc { get; set; }
    public List<DemonstrationScoreSheet> Sheets { get; set; } = [];
    public string? ReportText { get; set; }
    public int Recommendation { get; set; }
}

public sealed class DemonstrationScoreSheet
{
    public string ExpertUserId { get; set; } = string.Empty;
    public Dictionary<string, int> Scores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? Recommendation { get; set; }
    public string? Notes { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

public static class DemonstrationCriteria
{
    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        ("mastery", "Maîtrise de la matière"),
        ("clarity", "Clarté des explications"),
        ("pedagogy", "Pédagogie"),
        ("vulgarization", "Capacité à vulgariser"),
        ("interaction", "Interaction avec l'élève"),
        ("questions", "Gestion des questions"),
        ("organization", "Organisation du cours"),
        ("digitalTools", "Utilisation des outils numériques"),
        ("levelAdaptation", "Adaptation au niveau scolaire"),
        ("overall", "Qualité générale de la prestation")
    ];
}

public static class DemonstrationPayloadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static DemonstrationPayload Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DemonstrationPayload();
        try
        {
            return JsonSerializer.Deserialize<DemonstrationPayload>(json, Options)
                   ?? new DemonstrationPayload();
        }
        catch (JsonException)
        {
            return new DemonstrationPayload();
        }
    }

    public static string Serialize(DemonstrationPayload payload) =>
        JsonSerializer.Serialize(payload, Options);

    public static int? OverallPercent(DemonstrationPayload payload)
    {
        var sheets = payload.Sheets
            .Where(s => s.Scores is { Count: > 0 })
            .Select(s => s.Scores.Values.Where(v => v >= 0).DefaultIfEmpty().Average())
            .Where(a => a > 0)
            .ToList();
        if (sheets.Count == 0)
            return null;
        return (int)Math.Round(sheets.Average() * 10);
    }

    public static DemonstrationRecommendation RecommendationOf(string? json) =>
        (DemonstrationRecommendation)Parse(json).Recommendation;
}

public record UpdateWorkspacePayloadRequest(string? PayloadJson);
