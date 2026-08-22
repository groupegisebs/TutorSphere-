using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Api;
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
builder.Services.AddHostedService<OverduePaymentReminderService>();
builder.Services.AddHostedService<SubscriptionRenewalReminderService>();
builder.Services.AddHostedService<PackAccessReminderService>();
builder.Services.AddHostedService<SubscriptionPackExpiryService>();
builder.Services.AddHostedService<PendingPaymentSyncService>();
builder.Services.AddHostedService<PlatformLicenseExpiryService>();
builder.Services.AddHostedService<MeetingReminderService>();

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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        Exception current = ex ?? new InvalidOperationException("Erreur serveur.");
        while (current.InnerException is not null)
            current = current.InnerException;

        var isDb = ex is DbUpdateException;
        context.Response.StatusCode = isDb
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var message = isDb
            ? $"Enregistrement impossible : {current.Message}"
            : current.Message;
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});

{
    var payGw = app.Configuration.GetSection("PayGateway");
    var useSandbox = payGw.GetValue<bool?>("UseSandbox") ?? app.Environment.IsDevelopment() || app.Environment.IsStaging();
    app.Logger.LogWarning(
        "PayGateway Stripe mode au démarrage : {Mode} (UseSandbox={UseSandbox}, Env={Env})",
        useSandbox ? "DEV/TEST (bac à sable)" : "LIVE",
        payGw["UseSandbox"] ?? "(auto)",
        app.Environment.EnvironmentName);

    // Mail Sender = GiseMailSender (SecureMailGateway) — https://gisemailsender.gisebs.com
    var emailSection = app.Configuration.GetSection("Email");
    var emailBase = emailSection["BaseUrl"] ?? "";
    var emailKey = emailSection["ApiKey"] ?? "";
    var emailClient = emailSection["ClientCode"] ?? "TUTORSPHERE";
    var mailConfigured = !string.IsNullOrWhiteSpace(emailBase) && !string.IsNullOrWhiteSpace(emailKey);

    if (mailConfigured)
    {
        app.Logger.LogInformation(
            "Mail Sender configuré : BaseUrl={BaseUrl}, ClientCode={ClientCode}",
            emailBase.TrimEnd('/'),
            emailClient);
    }
    else if (app.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Mail Sender non configuré (Email:ApiKey / EMAIL__APIKEY manquant). " +
            "TutorSphere envoie les courriels via https://gisemailsender.gisebs.com — " +
            "définissez le secret GitHub TUTORSPHERE_EMAIL_API_KEY (client TUTORSPHERE dans GiseMailSender).");
    }
    else
    {
        app.Logger.LogWarning(
            "Mail Sender NON configuré — aucun e-mail ne sera envoyé. " +
            "Définissez Email:ApiKey (user-secrets) ou EMAIL__APIKEY. BaseUrl={BaseUrl}, ClientCode={ClientCode}",
            string.IsNullOrWhiteSpace(emailBase) ? "(vide)" : emailBase,
            emailClient);
    }
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseRequestLocalization();
app.UseCors();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
// Vidéos / fichiers uploadés (présentation d'offre, logos groupes, etc.).
// Served anonymously (before auth) so <img> from the Web origin / uploads BFF proxy can load them.
// Path MUST match the Docker volume mount (/app/uploads = ContentRoot/uploads).
{
    var contentTypes = new FileExtensionContentTypeProvider();
    // Ensure common image types (incl. SVG logos) are never served as octet-stream.
    contentTypes.Mappings[".png"] = "image/png";
    contentTypes.Mappings[".jpg"] = "image/jpeg";
    contentTypes.Mappings[".jpeg"] = "image/jpeg";
    contentTypes.Mappings[".gif"] = "image/gif";
    contentTypes.Mappings[".webp"] = "image/webp";
    contentTypes.Mappings[".svg"] = "image/svg+xml";
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = UploadsPaths.CreateFileProvider(app.Environment),
        RequestPath = UploadsPaths.RequestPath,
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = ctx =>
        {
            // Allow cross-origin <img> from tutorsphere.gisebs.com → api.tutorsphere…
            ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        }
    });
}
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapHub<ClassroomHub>("/hubs/classroom");
app.MapHub<MeetingHub>("/hubs/meeting");
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
