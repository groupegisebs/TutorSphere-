using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.Services;

public sealed class ConfigurationAppUrlProvider : IAppUrlProvider
{
    public ConfigurationAppUrlProvider(IConfiguration configuration)
    {
        WebBaseUrl = NormalizePublicUrl(
            configuration["WebBaseUrl"],
            "https://tutorsphere.gisebs.com");
        ApiPublicBaseUrl = NormalizePublicUrl(
            configuration["ApiBaseUrl"],
            "https://api.tutorsphere.gisebs.com");
    }

    public string WebBaseUrl { get; }
    public string ApiPublicBaseUrl { get; }

    public string BuildEmailConfirmUrl(string userId, string token) =>
        $"{WebBaseUrl}/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";

    /// <summary>
    /// Corrige les fautes de frappe fréquentes (qisebs / qiscbs → gisebs)
    /// et retire le slash final.
    /// </summary>
    public static string NormalizePublicUrl(string? configured, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        raw = raw
            .Replace("qisebs.com", "gisebs.com", StringComparison.OrdinalIgnoreCase)
            .Replace("qiscbs.com", "gisebs.com", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        return string.IsNullOrWhiteSpace(raw) ? fallback.TrimEnd('/') : raw;
    }
}
