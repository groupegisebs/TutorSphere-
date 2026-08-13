using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Admin;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Policies;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public interface IAdminUserProvisioningService
{
    Task<AdminCreatedAccountDto> CreateParentAsync(string adminUserId, AdminCreateParentRequest request, CancellationToken ct = default);
    Task<AdminCreatedAccountDto> CreateStudentAsync(string adminUserId, AdminCreateStudentRequest request, CancellationToken ct = default);
    Task<AdminCreatedAccountDto> CreateTeacherAsync(string adminUserId, AdminCreateTeacherRequest request, CancellationToken ct = default);
}

public sealed class AdminUserProvisioningService(
    UserManager<ApplicationUser> userManager,
    IApplicationDbContext db,
    IEmailService email,
    IAppUrlProvider urls,
    IConfiguration configuration,
    ILogger<AdminUserProvisioningService> logger,
    TutorSphere.Application.Services.ISubscriptionOfferingService offerings) : IAdminUserProvisioningService
{
    public async Task<AdminCreatedAccountDto> CreateParentAsync(
        string adminUserId,
        AdminCreateParentRequest request,
        CancellationToken ct = default)
    {
        var emailAddr = NormalizeEmail(request.Email);
        var firstName = RequireName(request.FirstName, "Prénom");
        var lastName = RequireName(request.LastName, "Nom");
        await EnsureEmailAvailableAsync(emailAddr);

        var password = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = emailAddr,
            Email = emailAddr,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = TrimOrNull(request.Phone),
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, UserRoles.Parent);

        var tenantId = await EnsureMarketplaceParentTenantIdAsync(ct);
        user.TenantId = tenantId;
        await userManager.UpdateAsync(user);

        if (!db.ParentProfilesForAnyTenant.Any(p => p.UserId == user.Id))
        {
            db.Add(new ParentProfile
            {
                TenantId = tenantId,
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = emailAddr,
                Phone = TrimOrNull(request.Phone)
            });
            await db.SaveChangesAsync(ct);
        }

        var sent = await SendCredentialsAsync(
            user, password, $"{urls.WebBaseUrl.TrimEnd('/')}/login/parent", "Espace parent", ct);

        logger.LogInformation("Admin {AdminId} a créé le parent {UserId} ({Email}).", adminUserId, user.Id, emailAddr);

        return new AdminCreatedAccountDto(
            user.Id, emailAddr, user.FullName, UserRoles.Parent, password, sent, tenantId);
    }

    public async Task<AdminCreatedAccountDto> CreateStudentAsync(
        string adminUserId,
        AdminCreateStudentRequest request,
        CancellationToken ct = default)
    {
        var emailAddr = NormalizeEmail(request.Email);
        var firstName = RequireName(request.FirstName, "Prénom");
        var lastName = RequireName(request.LastName, "Nom");
        await EnsureEmailAvailableAsync(emailAddr);

        var dob = request.DateOfBirth.Date;
        if (dob > DateTime.UtcNow.Date)
            throw new InvalidOperationException("La date de naissance ne peut pas être dans le futur.");
        var age = (int)((DateTime.Today - dob).TotalDays / 365.25);

        ParentProfile? parent = null;
        if (!string.IsNullOrWhiteSpace(request.ParentEmail))
        {
            var parentEmail = NormalizeEmail(request.ParentEmail);
            var parentUser = await userManager.FindByEmailAsync(parentEmail)
                ?? throw new InvalidOperationException("Parent introuvable pour cet e-mail.");
            parent = db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUser.Id)
                ?? throw new InvalidOperationException("Ce compte n'a pas de profil parent.");
        }
        else if (age < 14)
        {
            throw new InvalidOperationException(
                "Pour un élève de moins de 14 ans, indiquez l'e-mail d'un parent existant.");
        }

        var password = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = emailAddr,
            Email = emailAddr,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = TrimOrNull(request.Phone),
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, UserRoles.Student);

        var tenantId = parent?.TenantId ?? await EnsureMarketplaceParentTenantIdAsync(ct);
        user.TenantId = tenantId;
        await userManager.UpdateAsync(user);

        ParentProfile billingParent;
        if (parent is not null)
        {
            billingParent = parent;
        }
        else
        {
            // Élève autonome 14+ : profil parent miroir pour la facturation (même modèle que l'inscription publique).
            billingParent = new ParentProfile
            {
                TenantId = tenantId,
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = emailAddr,
                Phone = TrimOrNull(request.Phone)
            };
            db.Add(billingParent);
            await db.SaveChangesAsync(ct);
        }

        db.Add(new Student
        {
            TenantId = tenantId,
            UserId = user.Id,
            ParentProfileId = billingParent.Id,
            FirstName = firstName,
            LastName = lastName,
            Email = emailAddr,
            Phone = TrimOrNull(request.Phone),
            DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc),
            IsActive = true
        });
        await db.SaveChangesAsync(ct);

        var sent = await SendCredentialsAsync(
            user, password, $"{urls.WebBaseUrl.TrimEnd('/')}/login/eleve", "Espace élève", ct);

        logger.LogInformation("Admin {AdminId} a créé l'élève {UserId} ({Email}).", adminUserId, user.Id, emailAddr);

        return new AdminCreatedAccountDto(
            user.Id, emailAddr, user.FullName, UserRoles.Student, password, sent, tenantId);
    }

    public async Task<AdminCreatedAccountDto> CreateTeacherAsync(
        string adminUserId,
        AdminCreateTeacherRequest request,
        CancellationToken ct = default)
    {
        var emailAddr = NormalizeEmail(request.Email);
        var firstName = RequireName(request.FirstName, "Prénom");
        var lastName = RequireName(request.LastName, "Nom");
        await EnsureEmailAvailableAsync(emailAddr);

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == request.ExpertGroupId)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");
        if (!group.IsActive)
            throw new InvalidOperationException("Le groupe d'experts sélectionné n'est pas actif.");

        var schoolName = string.IsNullOrWhiteSpace(request.SchoolName)
            ? $"{firstName} {lastName}".Trim()
            : request.SchoolName.Trim();

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? AllocateUniqueSlug(schoolName)
            : NormalizeSlug(request.Slug!);
        if (db.Tenants.Any(t => t.Slug == slug))
            throw new InvalidOperationException("Cette adresse (slug) est déjà utilisée.");

        var country = ProfileVisibility.NormalizeCode(group.CountryCode);
        if (country.Length != 2)
            country = "CM";

        var password = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = emailAddr,
            Email = emailAddr,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = TrimOrNull(request.Phone),
            EmailConfirmed = true,
            MustChangePassword = true
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, UserRoles.Tutor);

        var now = DateTime.UtcNow;
        var activate = request.ActivateSchool;
        var tenant = new Tenant
        {
            Name = schoolName,
            Slug = slug,
            Subdomain = slug,
            City = TrimOrNull(request.City),
            Country = country,
            VisibleCountryCodes = ProfileVisibility.ToCsv(null, country),
            Status = activate ? TenantStatus.Active : TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            IsPublicProfile = activate,
            OwnerUserId = user.Id,
            Branding = new TenantBranding(),
            ExpertApprovalStatus = ExpertApprovalStatus.Approved,
            ApprovedByExpertGroupId = group.Id,
            ApprovedByUserId = adminUserId,
            ExpertApprovedAt = now,
            ExpertApprovalNotes = "Compte créé et affecté par un Super Admin.",
            TeacherConductPolicyVersion = TeacherConductPolicy.CurrentVersion,
            TeacherConductAcceptedAt = now,
            LicenseExpiresAt = activate ? now.AddYears(1) : null,
            OnboardingCompletedAt = activate ? now : null
        };
        db.Add(tenant);

        db.Add(new TeacherApplicationInvite
        {
            Email = emailAddr,
            FirstName = firstName,
            InvitedByUserId = adminUserId,
            ExpertGroupId = group.Id,
            Token = Guid.NewGuid().ToString("N"),
            SentAt = now,
            AcceptedAt = now,
            AcceptedTenantId = tenant.Id,
            Status = TeacherApplicationInviteStatus.Approved,
            ExpiresAt = now.AddDays(30)
        });

        await db.SaveChangesAsync(ct);

        user.TenantId = tenant.Id;
        await userManager.UpdateAsync(user);

        Guid? offeringId = null;
        if (request.InitialOffering is { } offerReq && !string.IsNullOrWhiteSpace(offerReq.Title))
        {
            var offering = await offerings.CreateForTenantAsync(tenant.Id, offerReq, ct);
            offeringId = offering.Id;
        }

        var loginUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/login/tuteur";
        var sent = await SendCredentialsAsync(user, password, loginUrl, group.Name, ct);

        logger.LogInformation(
            "Admin {AdminId} a créé l'enseignant {UserId} ({Email}) dans le groupe {GroupId}.",
            adminUserId, user.Id, emailAddr, group.Id);

        return new AdminCreatedAccountDto(
            user.Id, emailAddr, user.FullName, UserRoles.Tutor, password, sent,
            tenant.Id, tenant.Slug, group.Id, group.Name, offeringId);
    }

    private async Task<bool> SendCredentialsAsync(
        ApplicationUser user,
        string temporaryPassword,
        string loginUrl,
        string contextName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        try
        {
            await email.SendExpertInviteAsync(
                user.Email,
                string.IsNullOrWhiteSpace(user.FirstName) ? user.Email : user.FirstName,
                temporaryPassword,
                loginUrl,
                contextName,
                ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec envoi identifiants pour {Email}.", user.Email);
            // Fallback : lien de réinitialisation.
            try
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var webBase = (configuration["WebBaseUrl"] ?? urls.WebBaseUrl).TrimEnd('/');
                var resetUrl =
                    $"{webBase}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
                await email.SendResetPasswordAsync(user.Email, user.FirstName, resetUrl, ct);
                return true;
            }
            catch (Exception ex2)
            {
                logger.LogWarning(ex2, "Échec envoi lien reset pour {Email}.", user.Email);
                return false;
            }
        }
    }

    private async Task EnsureEmailAvailableAsync(string emailAddr)
    {
        if (await userManager.FindByEmailAsync(emailAddr) is not null)
            throw new InvalidOperationException("Un compte existe déjà avec cet e-mail.");
    }

    private async Task<Guid> EnsureMarketplaceParentTenantIdAsync(CancellationToken ct)
    {
        const string slug = "platform-parents";
        var existing = db.Tenants.Where(t => t.Slug == slug).Select(t => t.Id).FirstOrDefault();
        if (existing != Guid.Empty)
            return existing;

        var holding = new Tenant
        {
            Name = "TutorSphere Parents",
            Slug = slug,
            Subdomain = slug,
            Status = TenantStatus.Suspended,
            IsPublicProfile = false,
            OwnerUserId = string.Empty,
            Branding = new TenantBranding()
        };
        db.Add(holding);
        await db.SaveChangesAsync(ct);
        return holding.Id;
    }

    private string AllocateUniqueSlug(string schoolName)
    {
        var baseSlug = Regex.Replace(schoolName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "enseignant";
        if (baseSlug.Length > 40)
            baseSlug = baseSlug[..40].Trim('-');

        for (var i = 0; i < 20; i++)
        {
            var candidate = i == 0
                ? baseSlug
                : $"{baseSlug}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
            if (!db.Tenants.Any(t => t.Slug == candidate))
                return candidate;
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(48, baseSlug.Length + 9)];
    }

    private static string NormalizeSlug(string raw)
    {
        var slug = raw.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, @"^[a-z0-9]([a-z0-9-]{1,48}[a-z0-9])?$"))
            throw new InvalidOperationException("Sous-domaine invalide (lettres, chiffres, tirets).");
        return slug;
    }

    private static string NormalizeEmail(string email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(e) || !e.Contains('@', StringComparison.Ordinal))
            throw new InvalidOperationException("Adresse e-mail invalide.");
        return e;
    }

    private static string RequireName(string? value, string label)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            throw new InvalidOperationException($"{label} requis.");
        return v;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@%*?";
        Span<char> code = stackalloc char[12];
        code[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        code[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        code[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        code[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        const string all = upper + lower + digits + symbols;
        for (var i = 4; i < code.Length; i++)
            code[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        // Shuffle
        for (var i = code.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (code[i], code[j]) = (code[j], code[i]);
        }
        return new string(code);
    }
}
