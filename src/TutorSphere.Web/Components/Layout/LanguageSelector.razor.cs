using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using TutorSphere.Application.Common;
using TutorSphere.Web.Services;

namespace TutorSphere.Web.Components.Layout;

public partial class LanguageSelector : ComponentBase
{
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    [Inject]
    private CircuitCultureState CircuitCulture { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private string CurrentCulture { get; set; } = SupportedLanguageCodes.Default;

    protected internal static readonly LanguageOption[] Languages =
    [
        new(SupportedLanguageCodes.French, "Français", "🇫🇷"),
        new(SupportedLanguageCodes.English, "English", "🇬🇧"),
        new(SupportedLanguageCodes.Spanish, "Español", "🇪🇸"),
        new(SupportedLanguageCodes.German, "Deutsch", "🇩🇪"),
        new(SupportedLanguageCodes.Portuguese, "Português", "🇵🇹"),
        new(SupportedLanguageCodes.MandarinChinese, "中文", "🇨🇳"),
        new(SupportedLanguageCodes.Arabic, "العربية", "🇸🇦")
    ];

    protected override void OnInitialized() => SyncCurrentCulture();

    protected override void OnParametersSet() => SyncCurrentCulture();

    private void SyncCurrentCulture()
    {
        CultureRequestHelper.ApplyForUi(HttpContextAccessor.HttpContext, CircuitCulture);
        CurrentCulture = SupportedLanguageCodes.Normalize(
            CultureRequestHelper.ResolveForUi(HttpContextAccessor.HttpContext, CircuitCulture).Name);
    }

    private void OnCultureChanged(ChangeEventArgs e)
    {
        var culture = SupportedLanguageCodes.Normalize(e.Value?.ToString());
        if (culture == CurrentCulture)
            return;

        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var redirectPath = uri.PathAndQuery;
        if (string.IsNullOrWhiteSpace(redirectPath) || !redirectPath.StartsWith('/'))
            redirectPath = "/";

        // Full HTTP round-trip so UseRequestLocalization applies the new cookie.
        Navigation.NavigateTo(
            $"culture/set?culture={Uri.EscapeDataString(culture)}&redirectUri={Uri.EscapeDataString(redirectPath)}",
            forceLoad: true);
    }

    protected internal sealed record LanguageOption(string Code, string Label, string Flag);
}
