using System.Globalization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using TutorSphere.Application.Common;

namespace TutorSphere.Web.Services;

/// <summary>
/// Culture UI du circuit / requête, stockée en AsyncLocal pour survivre
/// aux changements de thread du renderer Blazor.
/// </summary>
public static class UiCultureContext
{
    private static readonly AsyncLocal<CultureInfo?> CurrentLocal = new();

    public static CultureInfo? Current
    {
        get => CurrentLocal.Value;
        set => CurrentLocal.Value = value;
    }

    public static void Set(CultureInfo culture)
    {
        Current = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}

/// <summary>
/// Culture capturée pour la durée du circuit InteractiveServer.
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
        UiCultureContext.Set(culture);
    }

    public void ApplyToCurrentThread() => UiCultureContext.Set(Culture);
}

/// <summary>
/// Applique la culture cookie à chaque activité inbound du circuit.
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
        if (httpContextAccessor.HttpContext is not null || !cultureState.IsCaptured)
            cultureState.Capture(CultureRequestHelper.Resolve(httpContextAccessor.HttpContext));
        else
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

    public static void ApplyToCurrentThread(HttpContext? httpContext) =>
        UiCultureContext.Set(Resolve(httpContext));

    public static CultureInfo ResolveForUi(
        HttpContext? httpContext,
        CircuitCultureState? circuitCulture)
    {
        if (UiCultureContext.Current is { } asyncCulture)
            return asyncCulture;

        if (circuitCulture is { IsCaptured: true })
            return circuitCulture.Culture;

        return Resolve(httpContext);
    }

    public static void ApplyForUi(HttpContext? httpContext, CircuitCultureState? circuitCulture)
    {
        if (httpContext is not null)
        {
            var culture = Resolve(httpContext);
            if (circuitCulture is not null)
                circuitCulture.Capture(culture);
            else
                UiCultureContext.Set(culture);
            return;
        }

        if (circuitCulture is { IsCaptured: true })
        {
            circuitCulture.ApplyToCurrentThread();
            return;
        }

        var fallback = CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);
        circuitCulture?.Capture(fallback);
        UiCultureContext.Set(fallback);
    }
}

/// <summary>
/// Localizer qui force la culture circuit/cookie à chaque lookup —
/// ne dépend plus uniquement de CurrentUICulture du thread pool.
/// </summary>
public sealed class CircuitCultureStringLocalizer<T> : IStringLocalizer<T>
{
    private readonly IStringLocalizer _inner;
    private readonly CircuitCultureState _cultureState;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CircuitCultureStringLocalizer(
        IStringLocalizerFactory factory,
        CircuitCultureState cultureState,
        IHttpContextAccessor httpContextAccessor)
    {
        _inner = factory.Create(typeof(T));
        _cultureState = cultureState;
        _httpContextAccessor = httpContextAccessor;
    }

    public LocalizedString this[string name] => Get(name);

    public LocalizedString this[string name, params object[] arguments] => Get(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        using (PushCulture())
            return _inner.GetAllStrings(includeParentCultures).ToList();
    }

    private LocalizedString Get(string name, params object[]? arguments)
    {
        using (PushCulture())
        {
            return arguments is { Length: > 0 }
                ? _inner[name, arguments]
                : _inner[name];
        }
    }

    private CultureScope PushCulture()
    {
        // Re-read cookie when HttpContext exists (SSR / negotiate); else use captured circuit culture.
        CultureInfo culture;
        if (_httpContextAccessor.HttpContext is not null)
        {
            culture = CultureRequestHelper.Resolve(_httpContextAccessor.HttpContext);
            _cultureState.Capture(culture);
        }
        else if (_cultureState.IsCaptured)
        {
            culture = _cultureState.Culture;
            UiCultureContext.Set(culture);
        }
        else if (UiCultureContext.Current is { } existing)
        {
            culture = existing;
        }
        else
        {
            culture = CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);
            _cultureState.Capture(culture);
        }

        return new CultureScope(culture);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;
        private readonly CultureInfo? _previousAsync;

        public CultureScope(CultureInfo culture)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            _previousAsync = UiCultureContext.Current;
            UiCultureContext.Set(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
            UiCultureContext.Current = _previousAsync;
        }
    }
}
