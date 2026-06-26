using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
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
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<HomeworkService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<AdminService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("TutorSphereApi"));
builder.Services.AddHealthChecks();

var app = builder.Build();

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

    await next();
});

app.UseRequestLocalization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();
