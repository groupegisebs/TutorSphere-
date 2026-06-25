using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.MultiTenancy;
using TutorSphere.Infrastructure.Persistence;
using TutorSphere.Infrastructure.Services;
using TutorSphere.Infrastructure.PayGateway;

namespace TutorSphere.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantContext>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMessageService, MessageService>();
        services.Configure<PayGatewaySettings>(configuration.GetSection(PayGatewaySettings.SectionName));
        services.AddHttpClient<PayGatewayClient>();
        services.AddScoped<IPaymentGatewayService, PayGatewayService>();
        services.AddApplication();

        return services;
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "admin@tutorsphere.com";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Super",
                LastName = "Admin",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, UserRoles.SuperAdmin);
        }

        await SeedPublicTutorsAsync(db);
    }

    private static async Task SeedPublicTutorsAsync(ApplicationDbContext db)
    {
        if (db.TenantsSet.Any(t => t.Slug == "marie-maths"))
            return;

        var tutors = new[]
        {
            new Tenant
            {
                Name = "Cours Marie Tremblay",
                Slug = "marie-maths",
                Subdomain = "marie-maths",
                Description = "Spécialiste en mathématiques pour le secondaire et le cégep.",
                City = "Montréal",
                Country = "CA",
                Language = "fr",
                Status = TenantStatus.Active,
                IsPublicProfile = true,
                Branding = new TenantBranding(),
                Offerings =
                [
                    new SubscriptionOffering
                    {
                        Title = "Forfait mathématiques",
                        Subject = "Mathématiques",
                        Price = 45m,
                        DurationDays = 30,
                        SessionCount = 4,
                        Mode = LessonMode.Online,
                        Conditions = "Secondaire 4-5",
                        IsActive = true
                    },
                    new SubscriptionOffering
                    {
                        Title = "Préparation examens",
                        Subject = "Mathématiques",
                        Price = 55m,
                        DurationDays = 30,
                        SessionCount = 4,
                        Mode = LessonMode.Hybrid,
                        Conditions = "Cégep",
                        IsActive = true
                    }
                ]
            },
            new Tenant
            {
                Name = "English with Sarah",
                Slug = "sarah-english",
                Subdomain = "sarah-english",
                Description = "Experienced ESL and high school English tutor.",
                City = "Québec",
                Country = "CA",
                Language = "en",
                Status = TenantStatus.Active,
                IsPublicProfile = true,
                Branding = new TenantBranding(),
                Offerings =
                [
                    new SubscriptionOffering
                    {
                        Title = "English conversation",
                        Subject = "English",
                        Price = 40m,
                        DurationDays = 30,
                        SessionCount = 4,
                        Mode = LessonMode.Online,
                        Conditions = "Secondary 3-5",
                        IsActive = true
                    }
                ]
            },
            new Tenant
            {
                Name = "Physique Pro",
                Slug = "physique-pro",
                Subdomain = "physique-pro",
                Description = "Cours de physique pour secondaire et cégep à Laval.",
                City = "Laval",
                Country = "CA",
                Language = "fr",
                Status = TenantStatus.Active,
                IsPublicProfile = true,
                Branding = new TenantBranding(),
                Offerings =
                [
                    new SubscriptionOffering
                    {
                        Title = "Physique en personne",
                        Subject = "Physique",
                        Price = 50m,
                        DurationDays = 30,
                        SessionCount = 4,
                        Mode = LessonMode.InPerson,
                        Conditions = "Secondaire 5",
                        IsActive = true
                    }
                ]
            }
        };

        db.TenantsSet.AddRange(tutors);
        await db.SaveChangesAsync();
    }
}
