using Microsoft.AspNetCore.Components;

namespace TutorSphere.Web.Services;

/// <summary>
/// Builds browser-safe URLs for API-hosted media (<c>/uploads/...</c>).
/// Never uses InternalApiBaseUrl (loopback) — that would break &lt;img&gt; in the browser.
/// Prefer same-origin relative paths so the Web BFF proxy can forward to the API.
/// </summary>
public static class MediaUrlHelper
{
    public static string Resolve(string? url, IConfiguration config, NavigationManager navigation)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        var trimmed = url.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // Same-origin relative path — Web hosts a /uploads proxy to the API.
        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("/uploads", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + trimmed.TrimStart('/');
        }

        // Other API-relative paths: use public ApiBaseUrl only (never loopback).
        var apiBase = NonEmpty(config["ApiBaseUrl"]);
        if (apiBase is not null)
            return $"{apiBase.TrimEnd('/')}/{trimmed.TrimStart('/')}";

        // Last resort: same-origin (works when Web proxies the path).
        return "/" + trimmed.TrimStart('/');
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
