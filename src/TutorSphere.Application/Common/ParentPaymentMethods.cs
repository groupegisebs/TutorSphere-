using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Payouts;

namespace TutorSphere.Application.Common;

/// <summary>
/// Moyens de paiement parent selon le pays du payeur.
/// Carte : partout. PayPal et Mobile Money : pays d'Afrique (offres XAF/XOF pour le mobile money).
/// </summary>
public static class ParentPaymentMethods
{
    public const string UnavailableInCountryMessage =
        "Ce moyen de paiement n'est pas proposé dans votre pays. Utilisez une carte bancaire.";

    public const string MobileMoneyCurrencyMessage =
        "Le paiement Mobile Money est disponible pour les offres en francs CFA (XAF ou XOF).";

    public readonly record struct Operator(string Code, string Label, bool Collectable);

    public static bool IsAfricanCurrency(string? currency) =>
        currency is not null
        && (currency.Equals("XAF", StringComparison.OrdinalIgnoreCase)
            || currency.Equals("XOF", StringComparison.OrdinalIgnoreCase));

    public static bool IsAfricanPayer(string? country, string? currency = null)
    {
        var code = NormalizeIso(country);
        if (code is not null)
            return TutorPayoutPolicy.AfricaCountries.Contains(code);
        return IsAfricanCurrency(currency);
    }

    public static bool AllowsPayPal(string? country, string? currency = null) =>
        IsAfricanPayer(country, currency);

    public static bool AllowsMobileMoney(string? country, string? currency = null) =>
        PaymentMethodCodes.MobileMoneyCollectionEnabled
        && IsAfricanPayer(country, currency)
        && (string.IsNullOrWhiteSpace(currency) || IsAfricanCurrency(currency));

    public static bool Allows(string? country, string? currency, string? method)
    {
        var m = PaymentMethodCodes.Normalize(method);
        if (m == PaymentMethodCodes.Card)
            return true;
        if (m == PaymentMethodCodes.PayPal)
            return AllowsPayPal(country, currency);
        if (PaymentMethodCodes.IsMobileMoney(m))
            return AllowsMobileMoney(country, currency);
        return false;
    }

    public static void EnsureAllowed(string? country, string? currency, string? method)
    {
        if (PaymentMethodCodes.IsDisabledCollectionChannel(method))
            throw new InvalidOperationException(PaymentMethodCodes.MobileMoneyCollectionDisabledMessage);

        if (!Allows(country, currency, method))
            throw new InvalidOperationException(UnavailableInCountryMessage);
    }

    public static IReadOnlyList<Operator> OperatorsFor(string? country)
    {
        var code = NormalizeIso(country) ?? "CM";
        var list = new List<Operator>();
        if (MtnCountries.Contains(code))
            list.Add(new("mtn", "MTN MoMo", true));
        if (OrangeCountries.Contains(code))
            list.Add(new("orange", "Orange Money", true));
        if (WaveCountries.Contains(code))
            list.Add(new("wave", "Wave", false));
        if (AirtelCountries.Contains(code))
            list.Add(new("airtel", "Airtel Money", false));

        if (!list.Any(o => o.Collectable))
        {
            list.Insert(0, new("orange", "Orange Money", true));
            list.Insert(0, new("mtn", "MTN MoMo", true));
        }

        return list;
    }

    public static string DefaultCollectableOperator(string? country) =>
        OperatorsFor(country).FirstOrDefault(o => o.Collectable).Code ?? PaymentMethodCodes.MtnMomo;

    /// <summary>ISO 2 lettres, ou null si inconnu — sans défaut Canada.</summary>
    public static string? NormalizeIso(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return null;

        var trimmed = country.Trim();
        if (trimmed.Length == 2)
            return trimmed.ToUpperInvariant();

        return TutorPayoutPolicy.NormalizeCountry(trimmed) is { Length: 2 } mapped
            ? mapped
            : null;
    }

    private static readonly HashSet<string> MtnCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "CM", "CI", "BJ", "GH", "GN", "CG", "GA", "NG", "UG", "RW", "ZA", "ZM", "SZ", "GW", "LR"
    };

    private static readonly HashSet<string> OrangeCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "CM", "CI", "SN", "ML", "BF", "NE", "TG", "GN", "CD", "MG", "MA", "TN", "BW", "SL", "LR"
    };

    private static readonly HashSet<string> WaveCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "SN", "CI", "ML", "BF", "TG", "GM"
    };

    private static readonly HashSet<string> AirtelCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "GA", "CD", "NE", "TD", "MG", "NG", "KE", "UG", "ZM", "RW"
    };
}
