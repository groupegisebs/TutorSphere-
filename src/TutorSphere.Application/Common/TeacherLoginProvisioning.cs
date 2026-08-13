using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TutorSphere.Application.Common;

/// <summary>
/// Compte enseignant provisionné : login = e-mail du groupe + « . » + code 4 chiffres unique,
/// avant le @ (ex. contact@tutorax.com → contact.4821@tutorax.com).
/// </summary>
public static class TeacherLoginProvisioning
{
    public const string DefaultPlatformOpsEmail = "admin@holotuto.com";

    public static string BuildLoginEmail(string groupEmail, int fourDigitCode)
    {
        var normalized = NormalizeEmail(groupEmail);
        var at = normalized.IndexOf('@');
        if (at <= 0 || at == normalized.Length - 1)
            throw new InvalidOperationException("E-mail du groupe invalide.");

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        // Nettoyer un éventuel suffixe .NNNN déjà présent.
        local = Regex.Replace(local, @"\.\d{4}$", "");
        return $"{local}.{fourDigitCode:D4}@{domain}";
    }

    public static string NormalizeEmail(string email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(e) || !e.Contains('@', StringComparison.Ordinal))
            throw new InvalidOperationException("Adresse e-mail invalide.");
        return e;
    }

    public static string? TryNormalizeOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return NormalizeEmail(email);
    }

    public static async Task<string> AllocateUniqueLoginEmailAsync(
        string groupEmail,
        Func<string, Task<bool>> emailTakenAsync,
        CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var code = RandomNumberGenerator.GetInt32(1000, 10000);
            var candidate = BuildLoginEmail(groupEmail, code);
            if (!await emailTakenAsync(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "Impossible de générer un e-mail de connexion unique pour ce groupe. Réessayez.");
    }

    public static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@%*?";
        Span<char> code = stackalloc char[12];
        code[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        code[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        code[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        code[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        const string all = upper + lower + digits + symbols;
        for (var i = 4; i < code.Length; i++)
            code[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var i = code.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (code[i], code[j]) = (code[j], code[i]);
        }
        return new string(code);
    }

    /// <summary>
    /// Destinataires des identifiants : toujours admin groupe + admin@holotuto.com ;
    /// + e-mail réel enseignant s'il a été fourni.
    /// </summary>
    public static IReadOnlyList<string> ResolveCredentialRecipients(
        string? teacherRealEmail,
        string? groupAdminEmail,
        string platformOpsEmail = DefaultPlatformOpsEmail)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(teacherRealEmail))
            set.Add(teacherRealEmail.Trim());
        if (!string.IsNullOrWhiteSpace(groupAdminEmail))
            set.Add(groupAdminEmail.Trim());
        if (!string.IsNullOrWhiteSpace(platformOpsEmail))
            set.Add(platformOpsEmail.Trim());
        return set.ToList();
    }
}
