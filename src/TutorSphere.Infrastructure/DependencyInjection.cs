using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("TutorSphere.Seed");
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Applying database migrations…");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}.", role);
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var includeDemoData = configuration.GetValue("Seed:IncludeDemoData", false);
        var removeLegacyBootstrap = configuration.GetValue("Seed:RemoveLegacyBootstrapUsers", true);

        // No hardcoded demo/bootstrap users. Real accounts come from registration or optional BootstrapAdmin.
        if (removeLegacyBootstrap)
            await RemoveLegacyBootstrapUsersAsync(userManager, db, logger);

        var bootstrapEnabled = configuration.GetValue("Seed:BootstrapAdmin:Enabled", false);
        if (bootstrapEnabled)
        {
            var email = configuration["Seed:BootstrapAdmin:Email"];
            var password = configuration["Seed:BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Seed:BootstrapAdmin:Enabled=true but Email/Password missing — skipped.");
            }
            else
            {
                await EnsureBootstrapAdminAsync(
                    userManager, logger,
                    email.Trim(),
                    password,
                    configuration["Seed:BootstrapAdmin:FirstName"] ?? "Admin",
                    configuration["Seed:BootstrapAdmin:LastName"] ?? "Platform");
            }
        }

        if (includeDemoData)
        {
            await SeedPublicTutorsAsync(db);
            await SeedDemoAccountsAsync(db, userManager);
            logger.LogInformation("Demo seed enabled (Seed:IncludeDemoData=true).");
        }
        else
        {
            await RemoveDemoDataAsync(db, userManager, logger);
        }

        await EnsureExistingParentProfilesAsync(db, userManager);

        var userCount = await db.Users.CountAsync();
        logger.LogInformation("Seed complete — {UserCount} user(s) in database (real data only).", userCount);
    }

    /// <summary>
    /// Removes obsolete hardcoded bootstrap accounts created by older TutorSphere builds.
    /// Does not touch real user emails (e.g. registered tutors).
    /// </summary>
    private static async Task RemoveLegacyBootstrapUsersAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger logger)
    {
        // Purely synthetic seed accounts from older versions — never real customers.
        string[] legacyEmails = ["admin@tutorsphere.com"];

        foreach (var email in legacyEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                continue;

            try
            {
                var ownedTenants = db.TenantsSet.Where(t => t.OwnerUserId == user.Id).ToList();
                foreach (var tenant in ownedTenants)
                    tenant.OwnerUserId = string.Empty;

                if (ownedTenants.Count > 0)
                    await db.SaveChangesAsync();

                var result = await userManager.DeleteAsync(user);
                if (result.Succeeded)
                    logger.LogInformation("Removed legacy seed user {Email}.", email);
                else
                    logger.LogWarning(
                        "Could not remove legacy seed user {Email}: {Errors}",
                        email,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed removing legacy seed user {Email}.", email);
            }
        }
    }

    /// <summary>
    /// Optional one-time platform admin when Seed:BootstrapAdmin:Enabled=true (empty DB / ops recovery).
    /// Never resets an existing password.
    /// </summary>
    private static async Task EnsureBootstrapAdminAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        string email,
        string password,
        string firstName,
        string lastName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            if (!await userManager.IsInRoleAsync(user, UserRoles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(user, UserRoles.SuperAdmin);
                logger.LogInformation("Bootstrap assigned SuperAdmin to existing {Email}.", email);
            }
            return;
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Bootstrap admin create failed for {Email}: {Errors}",
                email,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, UserRoles.SuperAdmin);
        logger.LogInformation("Bootstrap created SuperAdmin {Email}.", email);
    }

    private static async Task RemoveDemoDataAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        try
        {
            string[] demoEmails =
            [
                "sarah.anderson@demo.tutorsphere.com",
                "marie.tremblay@demo.tutorsphere.com",
                "emma.johnson@demo.tutorsphere.com",
                "lucas.anderson@demo.tutorsphere.com"
            ];

            string[] demoSlugs = ["marie-maths", "sarah-english", "physique-pro"];
            string[] demoNames =
            [
                "Cours Marie Tremblay",
                "English with Sarah",
                "Physique Pro"
            ];

            var removedUsers = 0;
            foreach (var email in demoEmails)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user is null) continue;

                var parentProfiles = db.ParentProfilesSet.Where(p => p.UserId == user.Id).ToList();
                if (parentProfiles.Count > 0)
                {
                    var parentIds = parentProfiles.Select(p => p.Id).ToList();
                    var students = db.StudentsSet
                        .Where(s => s.ParentProfileId.HasValue && parentIds.Contains(s.ParentProfileId.Value))
                        .ToList();
                    db.StudentsSet.RemoveRange(students);
                    db.ParentProfilesSet.RemoveRange(parentProfiles);
                }

                var orphanStudents = db.StudentsSet.Where(s => s.UserId == user.Id).ToList();
                if (orphanStudents.Count > 0)
                    db.StudentsSet.RemoveRange(orphanStudents);

                await userManager.DeleteAsync(user);
                removedUsers++;
            }

            var demoTenants = db.TenantsSet
                .Where(t => demoSlugs.Contains(t.Slug) || demoNames.Contains(t.Name))
                .ToList();

            if (demoTenants.Count > 0)
            {
                var tenantIds = demoTenants.Select(t => t.Id).ToList();

                // Delete in FK-safe order for demo public tutors.
                var subscriptions = db.StudentSubscriptionsSet
                    .Where(s => tenantIds.Contains(s.TenantId)).ToList();
                if (subscriptions.Count > 0)
                    db.StudentSubscriptionsSet.RemoveRange(subscriptions);

                var offerings = db.SubscriptionOfferingsSet
                    .Where(o => tenantIds.Contains(o.TenantId)).ToList();
                if (offerings.Count > 0)
                    db.SubscriptionOfferingsSet.RemoveRange(offerings);

                var brandings = db.TenantBrandingsSet
                    .Where(b => tenantIds.Contains(b.TenantId)).ToList();
                if (brandings.Count > 0)
                    db.TenantBrandingsSet.RemoveRange(brandings);

                var parentByTenant = db.ParentProfilesSet
                    .Where(p => tenantIds.Contains(p.TenantId)).ToList();
                if (parentByTenant.Count > 0)
                {
                    var parentIds = parentByTenant.Select(p => p.Id).ToList();
                    var linkedStudents = db.StudentsSet
                        .Where(s => s.ParentProfileId.HasValue && parentIds.Contains(s.ParentProfileId.Value))
                        .ToList();
                    if (linkedStudents.Count > 0)
                        db.StudentsSet.RemoveRange(linkedStudents);
                    db.ParentProfilesSet.RemoveRange(parentByTenant);
                }

                var studentsByTenant = db.StudentsSet
                    .Where(s => tenantIds.Contains(s.TenantId)).ToList();
                if (studentsByTenant.Count > 0)
                    db.StudentsSet.RemoveRange(studentsByTenant);

                var lessons = db.LessonsSet.Where(l => tenantIds.Contains(l.TenantId)).ToList();
                if (lessons.Count > 0)
                    db.LessonsSet.RemoveRange(lessons);

                var messages = db.MessagesSet.Where(m => tenantIds.Contains(m.TenantId)).ToList();
                if (messages.Count > 0)
                    db.MessagesSet.RemoveRange(messages);

                var documents = db.DocumentsSet.Where(d => tenantIds.Contains(d.TenantId)).ToList();
                if (documents.Count > 0)
                    db.DocumentsSet.RemoveRange(documents);

                db.TenantsSet.RemoveRange(demoTenants);
            }

            if (removedUsers > 0 || demoTenants.Count > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation(
                    "Demo data removed ({UserCount} user(s), {TenantCount} tenant(s)). Set Seed:IncludeDemoData=true to keep sample data.",
                    removedUsers, demoTenants.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fully remove demo seed data (FK constraints may remain).");
        }
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
        var fallbackTenant = db.TenantsSet.FirstOrDefault();
        if (fallbackTenant is null)
            return;

        var parentUserIds = new HashSet<string>();
        foreach (var role in UserRoles.ParentPortalRoles)
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(role))
                parentUserIds.Add(user.Id);
        }

        var added = false;
        foreach (var userId in parentUserIds)
        {
            if (db.ParentProfilesSet.Any(p => p.UserId == userId))
                continue;

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
                continue;

            var tenantId = user.TenantId
                ?? fallbackTenant.Id;

            db.ParentProfilesSet.Add(new ParentProfile
            {
                TenantId = tenantId,
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
