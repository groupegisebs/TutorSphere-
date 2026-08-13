using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using TutorSphere.Application.Common;
using TutorSphere.Web.Components;
using TutorSphere.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Resources live at Resources/SharedResources.resx → manifest name TutorSphere.Web.Resources.SharedResources.
// Do not set ResourcesPath here: it would make IStringLocalizer look under Resources/Resources/SharedResources.
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var localization = LocalizationSetup.CreateRequestLocalizationOptions();
    options.DefaultRequestCulture = localization.DefaultRequestCulture;
    options.SupportedCultures = localization.SupportedCultures;
    options.SupportedUICultures = localization.SupportedUICultures;
    options.ApplyCurrentCultureToResponseHeaders = localization.ApplyCurrentCultureToResponseHeaders;
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CircuitCultureState>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler,
    CultureCircuitHandler>();

// Replace default IStringLocalizer<> so every L["key"] uses cookie/circuit culture.
foreach (var d in builder.Services.Where(d => d.ServiceType == typeof(IStringLocalizer<>)).ToList())
    builder.Services.Remove(d);
builder.Services.AddScoped(typeof(IStringLocalizer<>), typeof(CircuitCultureStringLocalizer<>));
builder.Services.AddAuthorizationCore();
// Requis pour AuthorizeView sous @rendermode InteractiveServer
// (CascadingAuthenticationState dans App.razor ne traverse pas les frontières de rendu).
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ParentProfileState>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<HomeworkService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<MessagingNotificationState>();
builder.Services.AddScoped<RealtimeMessagingClient>();
builder.Services.AddScoped<RealtimeClassroomClient>();
builder.Services.AddScoped<AdminService>();
builder.Services.Configure<TutorSphere.Application.Options.ExpertModuleFeatureOptions>(
    builder.Configuration.GetSection(TutorSphere.Application.Options.ExpertModuleFeatureOptions.SectionName));
builder.Services.AddScoped<TutorSphere.Application.Services.IExpertModuleFeatureService,
    TutorSphere.Application.Services.ExpertModuleFeatureService>();
builder.Services.AddScoped<ExpertModuleFeatures>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    // Tolère les coupures data mobile (4G) avant d’abandonner le circuit.
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    options.DisconnectedCircuitMaxRetained = 200;
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
});

// Blazor Server HttpClient runs on the web host, not in the browser. Prefer InternalApiBaseUrl
// (loopback, e.g. http://127.0.0.1:55099) in production; ApiBaseUrl stays the public HTTPS URL
// for future browser-facing use once api.tutorsphere.gisebs.com is in NPM/DNS.
static string? NonEmptyConfig(IConfiguration configuration, string key)
{
    var value = configuration[key];
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

var apiBaseUrl = NonEmptyConfig(builder.Configuration, "InternalApiBaseUrl")
    ?? NonEmptyConfig(builder.Configuration, "ApiBaseUrl")
    ?? "http://localhost:5099";
builder.Services.AddHttpClient("TutorSphereApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddSingleton(new ApiConnectionInfo(apiBaseUrl));
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("TutorSphereApi"));
builder.Services.AddHealthChecks();

var app = builder.Build();

var apiInfo = app.Services.GetRequiredService<ApiConnectionInfo>();
app.Logger.LogInformation("TutorSphere Web — API backend: {ApiBaseUrl}", apiInfo.BaseUrl);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

var supportedCultureNames = SupportedLanguageCodes.All
    .Select(c => CultureInfo.GetCultureInfo(c).Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

// Cookie / Accept-Language → IRequestCultureFeature (must run before Blazor circuits).
app.UseRequestLocalization();

app.Use(async (context, next) =>
{
    var cultureFeature = context.Features.Get<IRequestCultureFeature>();
    var cultureName = cultureFeature?.RequestCulture.UICulture.Name;

    if (cultureName is not null && !supportedCultureNames.Contains(cultureName))
    {
        var fallback = CultureInfo.GetCultureInfo(SupportedLanguageCodes.Default);
        context.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture(fallback), cultureFeature!.Provider));
    }

    // Ensure CurrentUICulture is set for this HTTP request (SSR + Blazor negotiate).
    CultureRequestHelper.ApplyToCurrentThread(context);

    await next();
});

app.UseAntiforgery();

// PWA: keep SW + manifest + Digital Asset Links fresh; ensure MIME types.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/.well-known/assetlinks.json", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-cache";
    }

    await next();
});

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";
// Digital Asset Links: Google requires application/json for assetlinks.json.
contentTypeProvider.Mappings[".json"] = "application/json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/.well-known/assetlinks.json", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "application/json";
        }
    }
});

MapAuthBffEndpoints(app);
MapUploadsProxy(app);

app.MapGet("/culture/set", (HttpContext ctx, string culture, string? redirectUri) =>
{
    var code = SupportedLanguageCodes.Normalize(culture);
    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(code, code)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps
        });

    var target = string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri.Trim();
    if (!target.StartsWith('/') || target.StartsWith("//", StringComparison.Ordinal))
        target = "/";

    return Results.LocalRedirect(target);
});

app.MapGet("/culture/debug", (HttpContext ctx) =>
{
    ctx.Request.Cookies.TryGetValue(CookieRequestCultureProvider.DefaultCookieName, out var cookie);
    var feature = ctx.Features.Get<IRequestCultureFeature>();
    var resolved = CultureRequestHelper.Resolve(ctx);
    return Results.Json(new
    {
        cookie,
        featureCulture = feature?.RequestCulture.Culture.Name,
        featureUiCulture = feature?.RequestCulture.UICulture.Name,
        resolved = resolved.Name,
        threadCulture = CultureInfo.CurrentCulture.Name,
        threadUiCulture = CultureInfo.CurrentUICulture.Name,
        sampleFrKeyWouldNeedLocalizer = true
    });
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();

static void MapAuthBffEndpoints(WebApplication app)
{
    app.MapPost("/bff/auth/establish", (HttpContext ctx, EstablishAuthRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest();

        if (AuthService.IsJwtExpired(req.Token))
            return Results.Unauthorized();

        var expiresAt = req.ExpiresAt ?? AuthService.AuthResponseFromJwt(req.Token)?.ExpiresAt ?? DateTime.UtcNow.AddHours(24);
        ctx.Response.Cookies.Append(
            AuthCookieConstants.CookieName,
            req.Token,
            AuthService.BuildCookieOptions(expiresAt, ctx.Request.IsHttps));

        return Results.Ok();
    }).DisableAntiforgery();

    app.MapPost("/bff/auth/logout", (HttpContext ctx) =>
    {
        ctx.Response.Cookies.Delete(AuthCookieConstants.CookieName, new CookieOptions { Path = "/" });
        return Results.Ok();
    }).DisableAntiforgery();
}

/// <summary>
/// Browser &lt;img&gt; tags load from the Web origin. Uploaded files live on the API,
/// so proxy /uploads/* → InternalApiBaseUrl (loopback) without exposing that URL to clients.
/// </summary>
static void MapUploadsProxy(WebApplication app)
{
    app.MapGet("/uploads/{*filePath}", async (
        string filePath,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || filePath.Contains("..", StringComparison.Ordinal)
            || filePath.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(filePath))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var safePath = string.Join('/',
            filePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(safePath))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var client = httpClientFactory.CreateClient("TutorSphereApi");
        using var response = await client.GetAsync(
            $"uploads/{safePath}",
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            httpContext.Response.StatusCode = (int)response.StatusCode;
            return;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(safePath, out contentType))
                contentType = "application/octet-stream";
        }

        httpContext.Response.ContentType = contentType;
        httpContext.Response.Headers.CacheControl = "public,max-age=86400";
        await response.Content.CopyToAsync(httpContext.Response.Body, ct);
    }).AllowAnonymous();
}

internal sealed record EstablishAuthRequest(string Token, DateTime? ExpiresAt);

/// <summary>Resolved API base URL for server-side HttpClient calls.</summary>
internal sealed record ApiConnectionInfo(string BaseUrl);
