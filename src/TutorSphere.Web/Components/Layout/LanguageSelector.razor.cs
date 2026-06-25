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

    protected internal sealed record LanguageOption(string Code, string Label, string Flag);
}
