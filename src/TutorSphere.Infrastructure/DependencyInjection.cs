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
        var resetKnownPasswords = configuration.GetValue("Seed:ResetKnownAdminPasswords", true);
        var includeDemoData = configuration.GetValue("Seed:IncludeDemoData", false);

        await EnsureSeedAdminUserAsync(
            userManager, logger, resetKnownPasswords,
            "admin@tutorsphere.com", "Admin123!", "Super", "Admin", UserRoles.SuperAdmin);

        // Compte enseignant de bootstrap (espace tuteur + tenant).
        await EnsureSeedTutorUserAsync(
            userManager, db, logger, resetKnownPasswords,
            "bediga.jean@gisebs.com", "Mcd!35578", "Jean", "Bediga",
            tenantName: "École Jean Bediga",
            tenantSlug: "jean-bediga");

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
        logger.LogInformation("Seed complete — {UserCount} user(s) in database.", userCount);
    }

    /// <summary>
    /// Ensures bootstrap SuperAdmin accounts exist with the documented seed passwords.
    /// When <paramref name="resetPassword"/> is true, known accounts always get the seed password
    /// (recovery after a bad deploy or manual DB edits).
    /// </summary>
    private static async Task EnsureSeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        bool resetPassword,
        string email,
        string password,
        string firstName,
        string lastName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
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
                    "Seed failed to create {Email}: {Errors}",
                    email,
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Seed created {Email} with role {Role}.", email, role);
            return;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Seed confirmed email for {Email}.", email);
        }

        if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            logger.LogInformation("Seed unlocked {Email}.", email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Seed assigned role {Role} to {Email}.", role, email);
        }

        if (!resetPassword)
            return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, token, password);
        if (!resetResult.Succeeded)
        {
            logger.LogError(
                "Seed failed to reset password for {Email}: {Errors}",
                email,
                string.Join("; ", resetResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Seed reset password for {Email}.", email);
    }

    /// <summary>
    /// Ensures a Tutor account exists with an owned tenant (for calendar, students, etc.).
    /// If the email was previously seeded as SuperAdmin, converts it to Tutor.
    /// </summary>
    private static async Task EnsureSeedTutorUserAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger logger,
        bool resetPassword,
        string email,
        string password,
        string firstName,
        string lastName,
        string tenantName,
        string tenantSlug)
    {
        var tenant = db.TenantsSet.FirstOrDefault(t => t.Slug == tenantSlug);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = tenantName,
                Slug = tenantSlug,
                Subdomain = tenantSlug,
                Description = "Espace enseignant TutorSphere",
                Language = "fr",
                Status = TenantStatus.Active,
                IsPublicProfile = false,
                Branding = new TenantBranding()
            };
            db.TenantsSet.Add(tenant);
            await db.SaveChangesAsync();
            logger.LogInformation("Seed created tenant {Slug}.", tenantSlug);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                TenantId = tenant.Id
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Seed failed to create tutor {Email}: {Errors}",
                    email,
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, UserRoles.Tutor);
            tenant.OwnerUserId = user.Id;
            await db.SaveChangesAsync();
            logger.LogInformation("Seed created tutor {Email}.", email);
            return;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(user.FirstName))
        {
            user.FirstName = firstName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.LastName))
        {
            user.LastName = lastName;
            changed = true;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            changed = true;
        }

        if (user.TenantId != tenant.Id)
        {
            user.TenantId = tenant.Id;
            changed = true;
        }

        if (changed)
            await userManager.UpdateAsync(user);

        if (tenant.OwnerUserId != user.Id)
        {
            tenant.OwnerUserId = user.Id;
            await db.SaveChangesAsync();
        }

        // Prefer Tutor over leftover SuperAdmin from older seeds.
        if (await userManager.IsInRoleAsync(user, UserRoles.SuperAdmin))
        {
            await userManager.RemoveFromRoleAsync(user, UserRoles.SuperAdmin);
            logger.LogInformation("Seed removed SuperAdmin from {Email} (now Tutor).", email);
        }

        if (!await userManager.IsInRoleAsync(user, UserRoles.Tutor))
        {
            await userManager.AddToRoleAsync(user, UserRoles.Tutor);
            logger.LogInformation("Seed assigned Tutor to {Email}.", email);
        }

        if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow)
            await userManager.SetLockoutEndDateAsync(user, null);

        if (!resetPassword)
            return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, token, password);
        if (!resetResult.Succeeded)
        {
            logger.LogError(
                "Seed failed to reset password for tutor {Email}: {Errors}",
                email,
                string.Join("; ", resetResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Seed reset password for tutor {Email}.", email);
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

            var removedUsers = 0;
            foreach (var email in demoEmails)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user is null) continue;

                var parentProfiles = db.ParentProfilesSet.Where(p => p.UserId == user.Id).ToList();
                if (parentProfiles.Count > 0)
                {
                    var parentIds = parentProfiles.Select(p => p.Id).ToList();
                    var students = db.StudentsSet.Where(s => parentIds.Contains(s.ParentProfileId)).ToList();
                    db.StudentsSet.RemoveRange(students);
                    db.ParentProfilesSet.RemoveRange(parentProfiles);
                }

                var orphanStudents = db.StudentsSet.Where(s => s.UserId == user.Id).ToList();
                if (orphanStudents.Count > 0)
                    db.StudentsSet.RemoveRange(orphanStudents);

                await userManager.DeleteAsync(user);
                removedUsers++;
            }

            var demoTenants = db.TenantsSet.Where(t => demoSlugs.Contains(t.Slug)).ToList();
            if (demoTenants.Count > 0)
            {
                var tenantIds = demoTenants.Select(t => t.Id).ToList();
                var offerings = db.SubscriptionOfferingsSet.Where(o => tenantIds.Contains(o.TenantId)).ToList();
                if (offerings.Count > 0)
                    db.SubscriptionOfferingsSet.RemoveRange(offerings);

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
        var tenant = db.TenantsSet.FirstOrDefault(t => t.Slug == "marie-maths")
            ?? db.TenantsSet.FirstOrDefault();
        if (tenant is null)
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
