namespace TutorSphere.Application.Common;

/// <summary>Total d'une seule devise. Le code devise est toujours en majuscules.</summary>
public sealed record MoneyTotal(string Currency, decimal Amount, int Count);

/// <summary>
/// Agrégation de montants par devise. Une famille peut régler une offre en CAD et une autre en
/// XAF : additionner les deux donnerait un nombre qui ne veut rien dire, et l'étiqueter avec la
/// devise du premier paiement rencontré donnerait un montant faux dans une devise fausse. Aucune
/// conversion n'est faite ici, faute de taux de change dans l'application : les devises restent
/// séparées jusqu'à l'affichage.
/// </summary>
public static class MoneyTotals
{
    /// <summary>Devise retenue quand la source n'en donne aucune.</summary>
    public const string FallbackCurrency = "CAD";

    public static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? FallbackCurrency : currency.Trim().ToUpperInvariant();

    /// <summary>
    /// Regroupe par devise, du plus gros total au plus petit pour que l'affichage commence par
    /// le montant qui compte le plus.
    /// </summary>
    public static IReadOnlyList<MoneyTotal> Group<T>(
        IEnumerable<T> source,
        Func<T, decimal> amount,
        Func<T, string?> currency) =>
        [.. source
            .GroupBy(x => NormalizeCurrency(currency(x)), StringComparer.Ordinal)
            .Select(g => new MoneyTotal(
                g.Key,
                decimal.Round(g.Sum(amount), 2, MidpointRounding.AwayFromZero),
                g.Count()))
            .OrderByDescending(t => t.Amount)
            .ThenBy(t => t.Currency, StringComparer.Ordinal)];

    /// <summary>
    /// Rend « 120 CAD + 45 000 XAF ». Le séparateur « + » et non une virgule : il s'agit de
    /// montants qui s'ajoutent sans pouvoir se fondre, et la lecture doit le dire.
    /// </summary>
    public static string Format(IEnumerable<MoneyTotal> totals, string format = "N0")
    {
        var parts = totals
            .Where(t => t.Amount != 0m || t.Count > 0)
            .Select(t => $"{t.Amount.ToString(format)} {t.Currency}")
            .ToList();
        return parts.Count == 0 ? string.Empty : string.Join(" + ", parts);
    }
}
