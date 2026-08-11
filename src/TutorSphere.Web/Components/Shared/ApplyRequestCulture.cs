using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using TutorSphere.Web.Services;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Applique la culture HTTP (cookie) au thread courant.
/// Requis pour le SSR et en complément du <see cref="CultureCircuitHandler"/> InteractiveServer.
/// </summary>
public sealed class ApplyRequestCulture : ComponentBase
{
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    protected override void OnInitialized() => Apply();

    protected override void OnParametersSet() => Apply();

    private void Apply() => CultureRequestHelper.ApplyToCurrentThread(HttpContextAccessor.HttpContext);
}
