using System.Text.RegularExpressions;

namespace TutorSphere.Application.Common;

/// <summary>
/// Politique plateforme : les coordonnées des enseignants (e-mail, téléphone)
/// ne sont jamais publiques. Contact uniquement via la messagerie interne.
/// Les courriels transactionnels n'exposent que le nom d'affichage.
/// </summary>
public static partial class TeacherContactPrivacy
{
    public const string RedactedPlaceholder = "[contact via TutorSphere]";

    /// <summary>Retire e-mails et numéros de téléphone d'un texte destiné au public.</summary>
    public static string? RedactFromPublicText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var redacted = EmailPattern().Replace(text, RedactedPlaceholder);
        redacted = PhonePattern().Replace(redacted, RedactedPlaceholder);
        return redacted;
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return "••••@••••";

        var parts = email.Trim().Split('@', 2);
        var local = parts[0];
        var masked = local.Length <= 2 ? "••" : $"{local[0]}••••{local[^1]}";
        return $"{masked}@{parts[1]}";
    }

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "••••";

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return "••••";

        return $"+{digits[..Math.Min(3, digits.Length)]} ••• {digits[^2..]}";
    }

    /// <summary>
    /// Empêche d'injecter des clés de contact enseignant dans les données d'un template e-mail.
    /// </summary>
    public static void StripTeacherContactKeys(IDictionary<string, string> bodyData)
    {
        string[] forbidden =
        [
            "TutorEmail", "TeacherEmail", "OwnerEmail", "ContactEmail",
            "TutorPhone", "TeacherPhone", "OwnerPhone", "ContactPhone", "PhoneNumber"
        ];

        foreach (var key in forbidden)
            bodyData.Remove(key);
    }

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    // International / local: +1 514-555-1234, (514) 555 1234, 06 12 34 56 78, etc.
    [GeneratedRegex(@"(?<![\w])(?:\+?\d[\d\s.\-()]{6,}\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
