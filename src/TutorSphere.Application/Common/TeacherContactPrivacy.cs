using System.Text;
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

    /// <summary>Retire e-mails, téléphones, adresses et dates de naissance d'un texte public.</summary>
    public static string? RedactFromPublicText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var redacted = EmailPattern().Replace(text, RedactedPlaceholder);
        redacted = PhonePattern().Replace(redacted, RedactedPlaceholder);
        redacted = BirthDatePattern().Replace(redacted, RedactedPlaceholder);
        redacted = StreetAddressPattern().Replace(redacted, RedactedPlaceholder);
        redacted = PostalCodePattern().Replace(redacted, RedactedPlaceholder);
        return redacted;
    }

    /// <summary>True si le texte ressemble à une adresse résidentielle ou un code postal.</summary>
    public static bool ContainsResidentialDetails(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return StreetAddressPattern().IsMatch(text) || PostalCodePattern().IsMatch(text);
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

        string[] extra =
        [
            "BirthDate", "DateOfBirth", "Address", "FullAddress", "AddressLine1", "AddressLine2",
            "PostalCode", "EmergencyContact"
        ];
        foreach (var key in extra)
            bodyData.Remove(key);
    }

    /// <summary>Retire les clés personnelles d'un JSON de portfolio avant exposition publique.</summary>
    public static string? StripPortfolioPii(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            using var stream = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (IsForbiddenPortfolioKey(prop.Name))
                        continue;
                    prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static bool IsForbiddenPortfolioKey(string name) =>
        TeacherPublicPiiGuard.ForbiddenPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    // International / local: +1 514-555-1234, (514) 555 1234, 06 12 34 56 78, etc.
    [GeneratedRegex(@"(?<![\w])(?:\+?\d[\d\s.\-()]{6,}\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(
        @"\b(?:né(?:e)?\s+le\s+|born\s+(?:on\s+)?)?(?:0?[1-9]|[12]\d|3[01])[/\-.](?:0?[1-9]|1[0-2])[/\-.](?:19|20)\d{2}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BirthDatePattern();

    [GeneratedRegex(
        @"\b\d{1,4}\s+(?:rue|avenue|av\.?|boulevard|bd\.?|chemin|impasse|place|allée|street|st\.?|road|rd\.?|drive|dr\.?|lane|ln\.?)\b[^\n,]{0,60}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StreetAddressPattern();

    [GeneratedRegex(
        @"\b(?:[A-Z]\d[A-Z]\s?\d[A-Z]\d|\d{5}(?:-\d{4})?)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PostalCodePattern();
}
