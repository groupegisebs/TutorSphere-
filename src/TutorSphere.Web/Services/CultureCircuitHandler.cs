using System.Globalization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Localization;
using TutorSphere.Application.Common;

namespace TutorSphere.Web.Services;

/// <summary>
/// Applique la culture du cookie .AspNetCore.Culture au circuit InteractiveServer.
/// Sans cela, le layout SSR peut afficher FR pendant que la page interactive reste en EN
/// (culture du serveur / Accept-Language).
/// </summary>
public sealed class CultureCircuitHandler(IHttpContextAccessor httpContextAccessor) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        CultureRequestHelper.ApplyToCurrentThread(httpContextAccessor.HttpContext);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        CultureRequestHelper.ApplyToCurrentThread(httpContextAccessor.HttpContext);
        return Task.CompletedTask;
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
}
