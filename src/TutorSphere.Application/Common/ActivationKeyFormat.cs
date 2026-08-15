using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TutorSphere.Application.Common;

/// <summary>
/// Clé d'activation de licence enseignant au format TUTOR-MM-AAAAA-DD-UNIQUEGUID.
/// MM = mois de génération (2 chiffres), AAAAA = 5 caractères alphanumériques,
/// DD = jour (2 chiffres), UNIQUEGUID = Guid unique (32 hexadécimaux, sans tirets).
/// </summary>
public static class ActivationKeyFormat
{
    public const int MaxLength = 64;
    public const string Prefix = "TUTOR";
    public const string DisplayPattern = "TUTOR-MM-AAAAA-DD-UNIQUEGUID";

    /// <summary>Alphabet sans 0/O/1/I/L pour le segment AAAAA généré.</summary>
    private const string TokenAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly Regex KeyRegex = new(
        @"^TUTOR-(0[1-9]|1[0-2])-[A-Z0-9]{5}-(0[1-9]|[12]\d|3[01])-[A-F0-9]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Generate(DateTime? utcNow = null)
    {
        var d = utcNow ?? DateTime.UtcNow;
        var token = RandomToken(5);
        var uniqueGuid = Guid.NewGuid().ToString("N").ToUpperInvariant();
        return $"{Prefix}-{d.Month:00}-{token}-{d.Day:00}-{uniqueGuid}";
    }

    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        return KeyRegex.IsMatch(Normalize(code));
    }

    public static string Normalize(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

    public static void EnsureFormat(string code)
    {
        if (!IsValid(code))
            throw new InvalidOperationException(
                $"La clé d'activation doit respecter le format {DisplayPattern}.");
    }

    private static string RandomToken(int length)
    {
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = TokenAlphabet[bytes[i] % TokenAlphabet.Length];
        return new string(chars);
    }
}
