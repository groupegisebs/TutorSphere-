using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.JSInterop;
using System.Globalization;
using TutorSphere.Application.Common;

namespace TutorSphere.Web.Components.Layout;

public partial class LanguageSelector : ComponentBase
{
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    private string CurrentCulture { get; set; } = SupportedLanguageCodes.Default;

    private static readonly IReadOnlyList<(string Code, string Label, string Flag)> Languages =
    [
        (SupportedLanguageCodes.French, "Français", "🇫🇷"),
        (SupportedLanguageCodes.English, "English", "🇬🇧"),
        (SupportedLanguageCodes.Spanish, "Español", "🇪🇸"),
        (SupportedLanguageCodes.German, "Deutsch", "🇩🇪"),
        (SupportedLanguageCodes.Portuguese, "Português", "🇵🇹"),
        (SupportedLanguageCodes.MandarinChinese, "中文", "🇨🇳"),
        (SupportedLanguageCodes.Arabic, "العربية", "🇸🇦")
    ];

    protected override void OnInitialized()
    {
        var culture = HttpContextAccessor.HttpContext?.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture
            ?? CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);
        CurrentCulture = culture.Name;
    }

    private async Task OnCultureChanged(ChangeEventArgs e)
    {
        var culture = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(culture) || culture == CurrentCulture)
            return;

        await Js.InvokeVoidAsync("tutorSphereCulture.setCulture", culture);
    }
}
