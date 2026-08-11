using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using TutorSphere.Web.Services;

namespace TutorSphere.Web.Components.Shared;

/// <summary>
/// Applique la culture (cookie HTTP ou état du circuit) avant le rendu localisé.
/// </summary>
public sealed class ApplyRequestCulture : ComponentBase
{
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    [Inject]
    private CircuitCultureState CircuitCulture { get; set; } = default!;

    protected override void OnInitialized() => Apply();

    protected override void OnParametersSet() => Apply();

    private void Apply() =>
        CultureRequestHelper.ApplyForUi(HttpContextAccessor.HttpContext, CircuitCulture);
}
