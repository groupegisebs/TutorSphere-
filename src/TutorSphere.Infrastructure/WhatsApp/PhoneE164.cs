using System.Text;

namespace TutorSphere.Infrastructure.WhatsApp;

/// <summary>
/// Mise au format E.164 des numéros saisis à la main. La passerelle normalise déjà de son côté,
/// mais TutorSphere doit stocker une valeur canonique : sans cela, deux écritures du même numéro
/// donneraient deux inscriptions distinctes.
/// </summary>
public static class PhoneE164
{
    public static bool TryNormalize(
        string? input, string? defaultCountryCode, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Numéro de téléphone manquant.";
            return false;
        }

        var raw = input.Trim();
        var international = raw.StartsWith('+') || raw.StartsWith("00", StringComparison.Ordinal);

        var digits = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsDigit(c)) digits.Append(c);
        }

        var value = digits.ToString();
        if (international && value.StartsWith("00", StringComparison.Ordinal))
            value = value[2..];

        if (value.Length == 0)
        {
            error = "Numéro invalide : aucun chiffre exploitable.";
            return false;
        }

        if (!international)
        {
            var country = new string((defaultCountryCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (country.Length == 0)
            {
                error = "Saisissez le numéro au format international, par exemple +1 418 576 3462.";
                return false;
            }

            // Un numéro déjà préfixé de l'indicatif ne doit pas l'être deux fois : on ne complète
            // que les numéros de longueur nationale.
            if (!value.StartsWith(country, StringComparison.Ordinal) || value.Length <= 10)
                value = country + value.TrimStart('0');
        }

        if (value.Length is < 8 or > 15)
        {
            error = $"Numéro invalide : {value.Length} chiffres après mise en forme, 8 à 15 attendus.";
            return false;
        }

        normalized = value;
        return true;
    }

    /// <summary>Masque tout sauf l'indicatif et les quatre derniers chiffres (ex. 1••••••3462).</summary>
    public static string Mask(string? phoneE164)
    {
        if (string.IsNullOrWhiteSpace(phoneE164)) return string.Empty;
        if (phoneE164.Length <= 5) return new string('•', phoneE164.Length);

        var head = phoneE164[..1];
        var tail = phoneE164[^4..];
        return head + new string('•', phoneE164.Length - 5) + tail;
    }
}
