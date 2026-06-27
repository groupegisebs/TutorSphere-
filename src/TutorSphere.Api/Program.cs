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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            RoleClaimType = ClaimTypes.Role
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

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapHealthChecks("/health");

await DependencyInjection.SeedAsync(app.Services);

app.Run();
