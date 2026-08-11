using System.Globalization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Localization;
using TutorSphere.Application.Common;

namespace TutorSphere.Web.Services;

/// <summary>
/// Culture capturée une fois pour la durée du circuit InteractiveServer.
/// Survits quand <see cref="IHttpContextAccessor.HttpContext"/> devient null.
/// </summary>
public sealed class CircuitCultureState
{
    public CultureInfo Culture { get; private set; } =
        CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);

    public bool IsCaptured { get; private set; }

    public void Capture(CultureInfo culture)
    {
        Culture = culture;
        IsCaptured = true;
    }

    public void ApplyToCurrentThread()
    {
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
}

/// <summary>
/// Applique la culture cookie à chaque activité inbound du circuit
/// (événements UI, JS interop, premier UpdateRootComponents).
/// </summary>
public sealed class CultureCircuitHandler(
    IHttpContextAccessor httpContextAccessor,
    CircuitCultureState cultureState) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        CaptureAndApply();
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        CaptureAndApply();
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            CaptureAndApply();
            await next(context);
        };
    }

    private void CaptureAndApply()
    {
        // Prefer fresh cookie/feature when HttpContext is still available; else keep captured.
        if (httpContextAccessor.HttpContext is not null || !cultureState.IsCaptured)
        {
            cultureState.Capture(CultureRequestHelper.Resolve(httpContextAccessor.HttpContext));
        }

        cultureState.ApplyToCurrentThread();
    }
}

public static class CultureRequestHelper
{
    public static CultureInfo Resolve(HttpContext? httpContext)
    {
        var fromFeature = httpContext?.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture;
        if (fromFeature is not null)
            return CultureInfo.GetCultureInfo(SupportedLanguageCodes.Normalize(fromFeature.Name));

        if (httpContext?.Request.Cookies.TryGetValue(
                CookieRequestCultureProvider.DefaultCookieName, out var cookie) == true
            && !string.IsNullOrWhiteSpace(cookie))
        {
            var parsed = CookieRequestCultureProvider.ParseCookieValue(cookie);
            var ui = parsed?.UICultures.FirstOrDefault().Value;
            if (!string.IsNullOrWhiteSpace(ui))
                return CultureInfo.GetCultureInfo(SupportedLanguageCodes.Normalize(ui));
        }

        return CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);
    }

    public static void ApplyToCurrentThread(HttpContext? httpContext)
    {
        var culture = Resolve(httpContext);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Prefer circuit-captured culture (InteractiveServer), else HTTP cookie/feature (SSR).
    /// </summary>
    public static CultureInfo ResolveForUi(
        HttpContext? httpContext,
        CircuitCultureState? circuitCulture)
    {
        if (circuitCulture is { IsCaptured: true })
            return circuitCulture.Culture;

        return Resolve(httpContext);
    }

    public static void ApplyForUi(HttpContext? httpContext, CircuitCultureState? circuitCulture)
    {
        var culture = ResolveForUi(httpContext, circuitCulture);
        if (circuitCulture is not null && httpContext is not null && !circuitCulture.IsCaptured)
            circuitCulture.Capture(culture);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
