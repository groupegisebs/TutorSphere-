using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Common;

/// <summary>Niveau scolaire ciblé par une offre, rattaché à un cycle.</summary>
public sealed record SchoolLevelOption(string Code, string Label, SchoolCycle Cycle);

/// <summary>
/// Cycles et niveaux proposés aux offres de groupe. Les libellés restent volontairement génériques :
/// une même offre peut viser le Québec (Secondaire 1 à 5), la France ou l'Afrique francophone
/// (6e à Terminale) et les États-Unis (Grade 1 à 12), où la même classe ne porte pas le même nom.
/// Un découpage classe par classe obligerait à choisir un système scolaire et rendrait l'offre
/// illisible dans les autres.
/// </summary>
public static class SchoolLevelCatalog
{
    public static readonly IReadOnlyList<SchoolCycle> Cycles =
    [
        SchoolCycle.Primary,
        SchoolCycle.Secondary,
        SchoolCycle.University,
        SchoolCycle.AdultEducation
    ];

    public static readonly IReadOnlyList<SchoolLevelOption> Levels =
    [
        new("primaire", "Primaire", SchoolCycle.Primary),
        new("college", "Collège", SchoolCycle.Secondary),
        new("lycee", "Lycée", SchoolCycle.Secondary),
        new("universite", "Université", SchoolCycle.University),
        new("adultes", "Formation pour adultes", SchoolCycle.AdultEducation)
    ];

    public static string CycleLabel(SchoolCycle cycle) => cycle switch
    {
        SchoolCycle.Primary => "Primaire",
        SchoolCycle.Secondary => "Secondaire",
        SchoolCycle.University => "Universitaire",
        SchoolCycle.AdultEducation => "Formation pour adultes",
        _ => cycle.ToString()
    };

    /// <summary>Niveaux d'un cycle ; tous les niveaux si aucun cycle n'est choisi.</summary>
    public static IReadOnlyList<SchoolLevelOption> LevelsForCycle(SchoolCycle? cycle) =>
        cycle is null ? Levels : [.. Levels.Where(l => l.Cycle == cycle.Value)];

    public static string? LevelLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var normalized = code.Trim().ToLowerInvariant();
        return Levels.FirstOrDefault(l => l.Code == normalized)?.Label;
    }

    /// <summary>
    /// Rend « Collège, Lycée ». Les codes inconnus sont ignorés : un libellé de niveau inventé
    /// vaut moins que pas de libellé du tout.
    /// </summary>
    public static string FormatLevels(IEnumerable<string?>? codes)
    {
        var labels = NormalizeLevels(codes).Select(LevelLabel).Where(l => l is not null);
        return string.Join(", ", labels!);
    }

    /// <summary>Codes de niveau connus, sans doublon, dans l'ordre du catalogue.</summary>
    public static IReadOnlyList<string> NormalizeLevels(IEnumerable<string?>? codes)
    {
        if (codes is null)
            return [];

        var wanted = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return [.. Levels.Where(l => wanted.Contains(l.Code)).Select(l => l.Code)];
    }

    public static IReadOnlyList<string> ParseLevelCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : NormalizeLevels(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static string? ToLevelCsv(IEnumerable<string?>? codes)
    {
        var levels = NormalizeLevels(codes);
        return levels.Count == 0 ? null : string.Join(',', levels);
    }

    /// <summary>
    /// Le cycle est stocké en texte (colonne existante) : on n'y écrit que le nom de l'énumération,
    /// et on ignore toute valeur devenue inconnue plutôt que de la faire remonter telle quelle.
    /// </summary>
    public static SchoolCycle? ParseCycle(string? stored) =>
        Enum.TryParse<SchoolCycle>(stored, ignoreCase: true, out var cycle) && Cycles.Contains(cycle)
            ? cycle
            : null;

    public static string? ToStoredCycle(SchoolCycle? cycle) => cycle?.ToString();

    /// <summary>
    /// Retire les niveaux étrangers au cycle : garder « Université » sur un cycle primaire
    /// afficherait une offre incohérente au parent.
    /// </summary>
    public static IReadOnlyList<string> LevelsWithinCycle(IEnumerable<string?>? codes, SchoolCycle? cycle)
    {
        var normalized = NormalizeLevels(codes);
        if (cycle is null)
            return normalized;

        var allowed = LevelsForCycle(cycle).Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        return [.. normalized.Where(allowed.Contains)];
    }
}
