using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using TutorSphere.Application.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using TutorSphere.Api.BackgroundServices;
using TutorSphere.Api.Hubs;
using TutorSphere.Api.Services;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Infrastructure;
using TutorSphere.Infrastructure.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = SupportedLanguageCodes.Cultures;
    options.DefaultRequestCulture = new RequestCulture(SupportedLanguageCodes.Default);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR(options => options.AddFilter<TenantHubFilter>());
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
builder.Services.AddScoped<IRealTimeMessaging, SignalRMessageNotifier>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<LessonReminderService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "";
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key manquant ou trop court (min. 32 caractères). " +
        "Définissez JWT__KEY dans .env / secrets de déploiement.");
}

// AddIdentity (Infrastructure) registers cookie schemes as defaults first.
// AddAuthentication("Bearer") uses ??= and would NOT override them — JWT never ran.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // .NET 8+ defaults MapInboundClaims to false, leaving JWT claims as short names
        // ("sub", "role"). Controllers read ClaimTypes.NameIdentifier / Role — map inbound.
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/messages"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// Guarantee JWT remains the default even if Identity registers cookie schemes later.
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins is { Length: > 0 })
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

{
    var payGw = app.Configuration.GetSection("PayGateway");
    var useSandbox = payGw.GetValue<bool?>("UseSandbox") ?? app.Environment.IsDevelopment() || app.Environment.IsStaging();
    app.Logger.LogWarning(
        "PayGateway Stripe mode au démarrage : {Mode} (UseSandbox={UseSandbox}, Env={Env})",
        useSandbox ? "DEV/TEST (bac à sable)" : "LIVE",
        payGw["UseSandbox"] ?? "(auto)",
        app.Environment.EnvironmentName);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseRequestLocalization();
app.UseCors();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapHealthChecks("/health");

try
{
    await DependencyInjection.SeedAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TutorSphere.Startup");
    logger.LogCritical(ex, "Database migration or seed failed — API will not start.");
    throw;
}

app.Run();
