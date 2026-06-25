using System.Globalization;
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
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5099";
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
