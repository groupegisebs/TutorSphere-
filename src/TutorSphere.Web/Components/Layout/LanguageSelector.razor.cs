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

    // Absolute path (+ query) of the current page, used as the ?redirectUri= for /culture/set.
    // Many layouts that host this component (PublicLayout, etc.) are NOT @rendermode
    // InteractiveServer, so this component itself renders statically — there is no
    // live circuit to dispatch a C# @onchange event to. The <select> below is therefore
    // a plain HTML <form method="get"> with a native onchange="this.form.submit()",
    // which works identically whether the surrounding render is static or interactive.
    private string RedirectPath { get; set; } = "/";

    protected override void OnInitialized() => SyncCurrentCulture();

    protected override void OnParametersSet() => SyncCurrentCulture();

    private void SyncCurrentCulture()
    {
        CultureRequestHelper.ApplyForUi(HttpContextAccessor.HttpContext, CircuitCulture);
        CurrentCulture = SupportedLanguageCodes.Normalize(
            CultureRequestHelper.ResolveForUi(HttpContextAccessor.HttpContext, CircuitCulture).Name);

        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var redirectPath = uri.PathAndQuery;
        RedirectPath = string.IsNullOrWhiteSpace(redirectPath) || !redirectPath.StartsWith('/')
            ? "/"
            : redirectPath;
    }

    protected internal sealed record LanguageOption(string Code, string Label, string Flag);
}
