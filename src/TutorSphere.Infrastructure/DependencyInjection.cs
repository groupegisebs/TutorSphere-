using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Email;
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
        services.Configure<MailGatewaySettings>(configuration.GetSection(MailGatewaySettings.SectionName));
        services.AddHttpClient<MailGatewayClient>();
        services.AddScoped<IEmailService, EmailService>();
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

        const string superAdminEmail = "bediga.jean@gisebs.com";
        if (await userManager.FindByEmailAsync(superAdminEmail) is null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                FirstName = "Jean",
                LastName = "Bediga",
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(superAdmin, "Mcd!35578");
            if (createResult.Succeeded)
                await userManager.AddToRoleAsync(superAdmin, UserRoles.SuperAdmin);
        }

        await SeedPublicTutorsAsync(db);
        await SeedDemoAccountsAsync(db, userManager);
        await EnsureExistingParentProfilesAsync(db, userManager);
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

    /// <summary>
    /// Comptes démo pour tester les portails parent / élève / tuteur.
    /// Mot de passe commun : Demo123456!
    /// </summary>
    private static async Task SeedDemoAccountsAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        const string demoPassword = "Demo123456!";
        const string parentEmail = "sarah.anderson@demo.tutorsphere.com";
        const string tutorEmail = "marie.tremblay@demo.tutorsphere.com";
        const string emmaEmail = "emma.johnson@demo.tutorsphere.com";
        const string lucasEmail = "lucas.anderson@demo.tutorsphere.com";

        if (await userManager.FindByEmailAsync(parentEmail) is not null)
            return;

        var tenant = db.TenantsSet.FirstOrDefault(t => t.Slug == "marie-maths")
            ?? db.TenantsSet.FirstOrDefault();
        if (tenant is null)
            return;

        // ── Tuteur ──
        var tutorUser = new ApplicationUser
        {
            UserName = tutorEmail,
            Email = tutorEmail,
            FirstName = "Marie",
            LastName = "Tremblay",
            EmailConfirmed = true,
            TenantId = tenant.Id
        };
        if ((await userManager.CreateAsync(tutorUser, demoPassword)).Succeeded)
            await userManager.AddToRoleAsync(tutorUser, UserRoles.Tutor);

        // ── Parent Sarah Anderson ──
        var parentUser = new ApplicationUser
        {
            UserName = parentEmail,
            Email = parentEmail,
            FirstName = "Sarah",
            LastName = "Anderson",
            EmailConfirmed = true,
            TenantId = tenant.Id
        };
        if (!(await userManager.CreateAsync(parentUser, demoPassword)).Succeeded)
            return;

        await userManager.AddToRoleAsync(parentUser, UserRoles.Parent);

        var parentProfile = new ParentProfile
        {
            TenantId = tenant.Id,
            UserId = parentUser.Id,
            FirstName = "Sarah",
            LastName = "Anderson",
            Email = parentEmail,
            Phone = "+1 514-555-0100",
            City = "Montréal"
        };
        db.ParentProfilesSet.Add(parentProfile);
        await db.SaveChangesAsync();

        // ── Élèves Emma (13 ans) et Lucas (15 ans, autonome) ──
        var emmaUser = new ApplicationUser
        {
            UserName = emmaEmail,
            Email = emmaEmail,
            FirstName = "Emma",
            LastName = "Johnson",
            EmailConfirmed = true,
            TenantId = tenant.Id
        };
        if ((await userManager.CreateAsync(emmaUser, demoPassword)).Succeeded)
            await userManager.AddToRoleAsync(emmaUser, UserRoles.Student);

        var lucasUser = new ApplicationUser
        {
            UserName = lucasEmail,
            Email = lucasEmail,
            FirstName = "Lucas",
            LastName = "Anderson",
            EmailConfirmed = true,
            TenantId = tenant.Id
        };
        if ((await userManager.CreateAsync(lucasUser, demoPassword)).Succeeded)
            await userManager.AddToRoleAsync(lucasUser, UserRoles.Student);

        db.StudentsSet.AddRange(
            new Student
            {
                TenantId = tenant.Id,
                ParentProfileId = parentProfile.Id,
                UserId = emmaUser.Id,
                FirstName = "Emma",
                LastName = "Johnson",
                Email = emmaEmail,
                DateOfBirth = new DateTime(2012, 3, 12, 0, 0, 0, DateTimeKind.Utc),
                SchoolLevel = "Secondaire 2",
                SchoolName = "Collège Jean-de-Brébeuf",
                Subjects = "Mathématiques,Français,Sciences",
                IsActive = true
            },
            new Student
            {
                TenantId = tenant.Id,
                ParentProfileId = parentProfile.Id,
                UserId = lucasUser.Id,
                FirstName = "Lucas",
                LastName = "Anderson",
                Email = lucasEmail,
                DateOfBirth = new DateTime(2010, 8, 5, 0, 0, 0, DateTimeKind.Utc),
                SchoolLevel = "Secondaire 4",
                SchoolName = "Collège Jean-de-Brébeuf",
                Subjects = "Physique,Chimie,Anglais",
                IsActive = true
            });

        await db.SaveChangesAsync();
    }

    /// <summary>Rattache un profil parent aux comptes Parent déjà inscrits sans profil.</summary>
    private static async Task EnsureExistingParentProfilesAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var tenant = db.TenantsSet.FirstOrDefault(t => t.Slug == "marie-maths")
            ?? db.TenantsSet.FirstOrDefault();
        if (tenant is null)
            return;

        var parentUsers = await userManager.GetUsersInRoleAsync(UserRoles.Parent);
        var added = false;
        foreach (var user in parentUsers)
        {
            if (db.ParentProfilesSet.Any(p => p.UserId == user.Id))
                continue;

            db.ParentProfilesSet.Add(new ParentProfile
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? user.UserName ?? string.Empty
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }
}
