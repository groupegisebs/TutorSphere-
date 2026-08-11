using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using TutorSphere.Application.Common;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Applique la culture HTTP (cookie) au thread du circuit InteractiveServer,
/// sinon IStringLocalizer reste bloqué sur la culture du premier rendu.
/// </summary>
public sealed class ApplyRequestCulture : ComponentBase
{
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    protected override void OnInitialized() => Apply();

    protected override void OnParametersSet() => Apply();

    private void Apply()
    {
        var feature = HttpContextAccessor.HttpContext?.Features.Get<IRequestCultureFeature>();
        var culture = feature?.RequestCulture.UICulture
            ?? CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
