using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Auth;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Domain.Policies;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Identity;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginChildAsync(ChildLoginRequest request, CancellationToken ct = default);
    Task<ChildLoginAccessDto> EnableChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task<ChildLoginAccessDto> RegenerateChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task<ChildLoginAccessDto> GetChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task RevokeChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default);
    Task<RegisterSchoolResponse> RegisterSchoolAsync(RegisterSchoolRequest request, CancellationToken ct = default);
    Task<RegisterTeacherByExpertResponse> RegisterTeacherByExpertAsync(
        string expertUserId,
        RegisterTeacherByExpertRequest request,
        CancellationToken ct = default,
        Guid? actAsExpertGroupId = null);
    Task<TeacherInviteInfoResponse?> GetTeacherInviteInfoAsync(string token, CancellationToken ct = default);
    Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);
    Task ResendEmailConfirmationAsync(string email, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default);
    Task<AuthResponse> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task EnsureParentProfileForUserAsync(string userId, CancellationToken ct = default);
}

public class AuthService : IAuthService, ITeacherLoginIssuer
{
    private static readonly ConcurrentDictionary<string, DateTime> ConfirmResendCooldown = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan ConfirmResendMinInterval = TimeSpan.FromSeconds(60);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _email;
    private readonly IApplicationDbContext _db;
    private readonly IAppUrlProvider _urls;
    private readonly IExpertReviewNotificationService _expertNotify;
    private readonly ISubscriptionOfferingService _offerings;
    private readonly IParentEngagementService _parentEngagement;
    private readonly IExpertGroupManagerService _groupManagers;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IEmailService email,
        IApplicationDbContext db,
        IAppUrlProvider urls,
        IExpertReviewNotificationService expertNotify,
        ISubscriptionOfferingService offerings,
        IParentEngagementService parentEngagement,
        IExpertGroupManagerService groupManagers)
    {
        _userManager = userManager;
        _configuration = configuration;
        _email = email;
        _db = db;
        _urls = urls;
        _expertNotify = expertNotify;
        _offerings = offerings;
        _parentEngagement = parentEngagement;
        _groupManagers = groupManagers;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var role = NormalizeRole(request.Role);
        DateTime? studentDob = null;
        if (role == UserRoles.Student)
            studentDob = ValidateStudentRegistrationDob(request.DateOfBirth);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = SupportedLanguageCodes.Normalize(request.PreferredLanguage)
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, role);

        if (UserRoles.ParentPortalRoles.Contains(role))
        {
            string? country = null;
            if (role == UserRoles.Parent)
                country = FamilyResidence.RequireIso(request.Country);

            await EnsureParentProfileAsync(user, ct, country);
            if (role == UserRoles.Parent)
                await _parentEngagement.ApplyReferralCodeAsync(user.Id, request.ReferralCode, ct);
        }

        if (role == UserRoles.Student)
            await EnsureStudentProfileOnRegisterAsync(user, studentDob, ct);

        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        if (role == UserRoles.Parent)
        {
            // Espace parent : un seul e-mail d'invitation à valider le compte (pas de WELCOME « compte prêt »).
            var parentConfirmUrl = _urls.BuildEmailConfirmUrl(user.Id, confirmToken, "/login/parent?confirmed=true");
            await _email.SendParentAccessConfirmationAsync(user.Email!, user.FirstName, parentConfirmUrl, ct);
        }
        else
        {
            await _email.SendWelcomeAsync(user.Email!, user.FirstName, ct);
            var confirmUrl = _urls.BuildEmailConfirmUrl(user.Id, confirmToken);
            await _email.SendEmailConfirmationSimpleAsync(user.Email!, user.FirstName, confirmUrl, ct);
        }

        // Pas de JWT tant que l'e-mail n'est pas confirmé (évite un accès API avant validation).
        return new AuthResponse(
            string.Empty,
            user.Email ?? string.Empty,
            user.FullName,
            role,
            user.TenantId,
            DateTime.UtcNow,
            null);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Ce compte est désactivé. Contactez l'administrateur.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = ResolvePrimaryRole(roles);

        // Admins plateforme : pas de blocage « e-mail non confirmé » (bootstrap / ops).
        var isPlatformAdmin = role is UserRoles.SuperAdmin or UserRoles.PlatformAdmin;
        if (!isPlatformAdmin && !await _userManager.IsEmailConfirmedAsync(user))
            throw new EmailNotConfirmedException();

        if (!await _userManager.CheckPasswordAsync(user, password))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        if (roles.Contains(UserRoles.Tutor) && user.TenantId is Guid tenantId)
        {
            var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
            if (tenant is not null
                && tenant.ExpertApprovalStatus is not ExpertApprovalStatus.Approved)
            {
                throw new UnauthorizedAccessException(
                    "Votre candidature est en cours d'examen. Vous recevrez un e-mail d'acceptation avec vos identifiants de connexion.");
            }
        }

        // Groupe expert : Expert ou Responsable de groupe.
        if (role is UserRoles.Expert or UserRoles.GroupManager)
            EnsureExpertGroupIsActive(user.Id);

        // Profil parent si le compte a le rôle Parent (même si le rôle JWT principal est autre).
        if (role is not (UserRoles.SuperAdmin or UserRoles.PlatformAdmin)
            && (UserRoles.ParentPortalRoles.Contains(role) || roles.Contains(UserRoles.Parent)))
            await EnsureParentProfileAsync(user, ct);

        return await BuildAuthResponse(user, role, roles);
    }

    public async Task<AuthResponse> LoginChildAsync(ChildLoginRequest request, CancellationToken ct = default)
    {
        var parentEmail = request.ParentEmail.Trim();
        var accessCode = request.AccessCode.Trim();
        if (string.IsNullOrWhiteSpace(parentEmail) || string.IsNullOrWhiteSpace(accessCode))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var parentUser = await _userManager.FindByEmailAsync(parentEmail)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUser.Id)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        var codeNorm = accessCode.Trim();
        var student = _db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parent.Id && s.IsActive && s.LoginAccessCode != null)
            .AsEnumerable()
            .FirstOrDefault(s =>
                string.Equals(s.LoginAccessCode, codeNorm, StringComparison.Ordinal));

        if (student is null || string.IsNullOrEmpty(student.UserId))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var user = await _userManager.FindByIdAsync(student.UserId)
            ?? throw new UnauthorizedAccessException("Identifiants invalides.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Ce compte est désactivé. Contactez votre parent.");

        if (!await _userManager.CheckPasswordAsync(user, codeNorm))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(r => r == UserRoles.Student) ?? UserRoles.Student;
        return await BuildAuthResponse(user, role, roles);
    }

    public Task<ChildLoginAccessDto> EnableChildLoginAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct = default) =>
        ProvisionOrRegenerateChildAccessAsync(parentUserId, studentId, ct);

    public Task<ChildLoginAccessDto> RegenerateChildLoginAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct = default) =>
        ProvisionOrRegenerateChildAccessAsync(parentUserId, studentId, ct);

    public async Task<ChildLoginAccessDto> GetChildLoginAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var student = await GetOwnedChildAsync(parentUserId, studentId, ct);
        var hasAccess = !string.IsNullOrEmpty(student.UserId);
        if (!hasAccess)
            return new ChildLoginAccessDto(student.Id, false, null, null);

        var parent = _db.ParentProfilesForAnyTenant.First(p => p.UserId == parentUserId);
        var hint = string.IsNullOrWhiteSpace(student.Email)
            ? $"Connexion : e-mail du parent ({parent.Email}) + ce code"
            : $"Connexion : e-mail du parent ({parent.Email}) + ce code, ou le courriel de l'enfant avec le code";

        return new ChildLoginAccessDto(student.Id, true, student.LoginAccessCode, hint);
    }

    public async Task RevokeChildLoginAccessAsync(string parentUserId, Guid studentId, CancellationToken ct = default)
    {
        var student = await GetOwnedChildAsync(parentUserId, studentId, ct);

        if (!string.IsNullOrEmpty(student.UserId))
        {
            var user = await _userManager.FindByIdAsync(student.UserId);
            if (user is not null)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
        }

        student.UserId = null;
        student.LoginAccessCode = null;
        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ChildLoginAccessDto> ProvisionOrRegenerateChildAccessAsync(
        string parentUserId,
        Guid studentId,
        CancellationToken ct)
    {
        var student = await GetOwnedChildAsync(parentUserId, studentId, ct);
        var accessCode = GenerateChildAccessCode();

        ApplicationUser user;
        if (!string.IsNullOrEmpty(student.UserId))
        {
            user = await _userManager.FindByIdAsync(student.UserId)
                ?? throw new InvalidOperationException("Compte de connexion introuvable pour cet enfant.");

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.SetLockoutEnabledAsync(user, false);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, accessCode);
            if (!reset.Succeeded)
                throw new InvalidOperationException(string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
        else
        {
            var loginEmail = await ResolveChildIdentityEmailAsync(student, ct);
            user = new ApplicationUser
            {
                UserName = loginEmail,
                Email = loginEmail,
                FirstName = student.FirstName,
                LastName = student.LastName,
                EmailConfirmed = true,
                TenantId = student.TenantId
            };

            var create = await _userManager.CreateAsync(user, accessCode);
            if (!create.Succeeded)
                throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, UserRoles.Student);
            student.UserId = user.Id;
        }

        student.LoginAccessCode = accessCode;
        student.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var parent = _db.ParentProfilesForAnyTenant.First(p => p.UserId == parentUserId);
        var hint = string.IsNullOrWhiteSpace(student.Email)
            ? $"Connexion : e-mail du parent ({parent.Email}) + ce code"
            : $"Connexion : e-mail du parent ({parent.Email}) + ce code, ou le courriel de l'enfant avec le code";

        return new ChildLoginAccessDto(student.Id, true, accessCode, hint);
    }

    private async Task<Student> GetOwnedChildAsync(string parentUserId, Guid studentId, CancellationToken ct)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == studentId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        await Task.CompletedTask;
        return student;
    }

    private async Task<string> ResolveChildIdentityEmailAsync(Student student, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(student.Email))
        {
            var email = student.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is null)
                return email;
        }

        // E-mail synthétique unique : un parent peut avoir plusieurs enfants sans adresse.
        return $"child.{student.Id:N}@child.tutorsphere.local";
    }

    /// <summary>Code 8 caractères respectant la politique Identity (digit + length 8 + maj/min/symbole).</summary>
    private static string GenerateChildAccessCode()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        Span<char> code = stackalloc char[8];
        code[0] = upper[Random.Shared.Next(upper.Length)];
        code[1] = upper[Random.Shared.Next(upper.Length)];
        code[2] = digits[Random.Shared.Next(digits.Length)];
        code[3] = digits[Random.Shared.Next(digits.Length)];
        code[4] = digits[Random.Shared.Next(digits.Length)];
        code[5] = digits[Random.Shared.Next(digits.Length)];
        code[6] = lower[Random.Shared.Next(lower.Length)];
        code[7] = '!';
        return new string(code);
    }

    public async Task EnsureParentProfileForUserAsync(string userId, CancellationToken ct = default)
    {
        if (_db.ParentProfilesForAnyTenant.Any(p => p.UserId == userId))
            return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return;

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(r => UserRoles.ParentPortalRoles.Contains(r)))
            return;

        await EnsureParentProfileAsync(user, ct);
    }

    public async Task<RegisterSchoolResponse> RegisterSchoolAsync(RegisterSchoolRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.InviteToken))
            throw new InvalidOperationException(
                "L'inscription enseignant se fait uniquement sur invitation. Soumettez une demande d'intérêt ou utilisez le lien reçu.");

        var inviteToken = request.InviteToken.Trim();
        var invite = _db.TeacherApplicationInvites.FirstOrDefault(i => i.Token == inviteToken)
            ?? throw new InvalidOperationException("Invitation invalide ou introuvable.");
        if (invite.Status != TeacherApplicationInviteStatus.Sent)
            throw new InvalidOperationException("Cette invitation n'est plus valide.");
        if (invite.ExpiresAt is DateTime exp && exp < DateTime.UtcNow)
        {
            invite.Status = TeacherApplicationInviteStatus.Expired;
            invite.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Cette invitation a expiré.");
        }

        var inviteEmail = (invite.Email ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(inviteEmail)
            && !string.Equals(inviteEmail, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Utilisez l'adresse e-mail à laquelle l'invitation a été envoyée.");

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, @"^[a-z0-9]([a-z0-9-]{1,48}[a-z0-9])?$"))
            throw new InvalidOperationException("Sous-domaine invalide.");

        var inviteGroup = _db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId);

        if (!request.AcceptedTeacherConductPolicy
            || !TutorSphere.Domain.Policies.TeacherConductPolicy.IsCurrent(request.TeacherConductPolicyVersion))
        {
            throw new InvalidOperationException(
                "Vous devez accepter le Code de conduite et d'éthique enseignant (version en vigueur) pour créer un compte.");
        }

        AddressCoherence.ThrowIfInvalid(
            request.Country, request.City, request.PostalCode, request.Address, request.Phone);

        var existingUser = await _userManager.FindByEmailAsync(request.Email.Trim());
        ApplicationUser user;
        if (existingUser is not null)
        {
            var existingRoles = await _userManager.GetRolesAsync(existingUser);
            var isTeacher = existingRoles.Contains(UserRoles.Tutor)
                || existingRoles.Contains(UserRoles.TeachingAssistant);
            if (!isTeacher)
                throw new InvalidOperationException(
                    "Cette adresse e-mail est déjà utilisée par un autre type de compte.");

            var existingTenant = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == existingUser.Id)
                ?? (existingUser.TenantId is Guid tid
                    ? _db.Tenants.FirstOrDefault(t => t.Id == tid)
                    : null);
            if (existingTenant is not null)
                return await UpdateExistingTeacherFromInviteAsync(
                    existingUser, existingTenant, invite, request, inviteGroup, ct);

            user = existingUser;
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = NormalizeOptionalPhone(request.Phone);
            user.PreferredLanguage = TeacherCommunicationLanguages.Primary(
                TeacherCommunicationLanguages.NormalizeMany(request.CommunicationLanguages, request.PreferredLanguage));
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            if (!existingRoles.Contains(UserRoles.Tutor))
                await _userManager.AddToRoleAsync(user, UserRoles.Tutor);
        }
        else
        {
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PhoneNumber = NormalizeOptionalPhone(request.Phone),
                PreferredLanguage = TeacherCommunicationLanguages.Primary(
                    TeacherCommunicationLanguages.NormalizeMany(request.CommunicationLanguages, request.PreferredLanguage)),
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var holdPassword = TeacherLoginProvisioning.GenerateTemporaryPassword();
            var result = await _userManager.CreateAsync(user, holdPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, UserRoles.Tutor);
        }

        if (_db.Tenants.Any(t => t.Slug == slug))
            throw new InvalidOperationException("Cette adresse est déjà utilisée par un autre profil.");

        var commLanguages = TeacherCommunicationLanguages.NormalizeMany(
            request.CommunicationLanguages, request.PreferredLanguage);
        var language = TeacherCommunicationLanguages.Primary(commLanguages);

        var country = ProfileVisibility.NormalizeCode(request.Country);
        if (country.Length != 2)
            country = ProfileVisibility.NormalizeCode(inviteGroup?.CountryCode);
        // Un groupe sans pays n'en impose aucun : mieux vaut un pays vide, que l'enseignant
        // complétera, qu'un « CA » inventé qui fausserait fuseau horaire et visibilité.
        if (country.Length != 2 && inviteGroup is null)
            country = "CA";

        var timeZone = TimeZoneCatalog.Normalize(
            string.IsNullOrWhiteSpace(request.TimeZone)
                ? TimeZoneCatalog.DefaultForCountry(country)
                : request.TimeZone);

        GroupOffer? catalogOffer = null;
        if (request.GroupOfferId is Guid offerId && offerId != Guid.Empty)
        {
            catalogOffer = _db.GroupOffers.FirstOrDefault(o =>
                o.Id == offerId
                && o.ExpertGroupId == invite.ExpertGroupId
                && o.Status == GroupOfferStatus.Published)
                ?? throw new InvalidOperationException("Offre du groupe introuvable ou non publiée.");
        }

        var currency = catalogOffer is not null
            ? catalogOffer.Currency
            : GroupOfferCurrencyRules.ResolveCurrency(country);

        var presentation = TeacherContactPrivacy.RedactFromPublicText(request.Presentation?.Trim());
        var privateNotes = BuildPrivateProfileNotes(request.Address, request.PostalCode, request.DateOfBirth);

        var tenant = new Tenant
        {
            Name = request.SchoolName.Trim(),
            Slug = slug,
            Subdomain = slug,
            Description = privateNotes,
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            Country = country,
            VisibleCountryCodes = ProfileVisibility.ToCsv(
                catalogOffer?.IsInternational == true && !string.IsNullOrWhiteSpace(catalogOffer.MarketCountryCode)
                    ? new[] { catalogOffer.MarketCountryCode }
                    : null,
                country),
            TimeZone = timeZone,
            Currency = currency,
            Language = language,
            CommunicationLanguagesCsv = TeacherCommunicationLanguages.ToCsv(commLanguages),
            Status = TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            OwnerUserId = user.Id,
            Branding = new TenantBranding
            {
                Presentation = presentation,
                Portfolio = TeacherOnboardingPortfolio.Build(
                    null,
                    commLanguages,
                    request.YearsExperience,
                    request.Diplomas,
                    request.Certifications)
            },
            TeacherConductPolicyVersion = TeacherConductPolicy.CurrentVersion,
            TeacherConductAcceptedAt = DateTime.UtcNow,
            ExpertApprovalStatus = ExpertApprovalStatus.Pending,
            ReviewPriority = string.IsNullOrWhiteSpace(invite.Email) ? 1 : 0,
            ApprovedByExpertGroupId = invite.ExpertGroupId
        };

        _db.Add(tenant);
        await _db.SaveChangesAsync(ct);

        await MarkInviteAcceptedIfAnyAsync(request.Email, tenant.Id, request.InviteToken, ct);
        await RecordOpenInviteSignupAsync(invite, tenant, user.Id, request.Email, request.FirstName, request.LastName, ct);

        user.TenantId = tenant.Id;
        user.TimeZone = timeZone;
        await _userManager.UpdateAsync(user);

        SaveTeacherAvailabilities(tenant.Id, request.Availabilities, request.InitialOffering?.Schedule);
        await _db.SaveChangesAsync(ct);

        var offering = await TryCreateInitialOfferingAsync(
            tenant.Id,
            catalogOffer,
            request.InitialOffering,
            currency,
            timeZone,
            ct);

        if (catalogOffer is not null && offering is not null)
        {
            _db.Add(new GroupOfferTeacher
            {
                GroupOfferId = catalogOffer.Id,
                TeacherTenantId = tenant.Id,
                AssignmentStatus = GroupOfferTeacherAssignmentStatus.Applied,
                TeacherPrice = offering.Price,
                SubscriptionOfferingId = offering.Id
            });
            await _db.SaveChangesAsync(ct);
        }

        await _expertNotify.NotifyExpertsIfNeededAsync(tenant.Id, ct);

        return new RegisterSchoolResponse(tenant.Id, tenant.Slug, user.Email!);
    }

    private async Task RecordOpenInviteSignupAsync(
        TeacherApplicationInvite openInvite,
        Tenant tenant,
        string teacherUserId,
        string email,
        string firstName,
        string lastName,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(openInvite.Email))
            return;

        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var already = _db.TeacherApplicationInvites.Any(i =>
            i.AcceptedTenantId == tenant.Id
            && i.ExpertGroupId == openInvite.ExpertGroupId
            && i.Status == TeacherApplicationInviteStatus.Registered);
        if (!already)
        {
            _db.Add(new TeacherApplicationInvite
            {
                Email = normalized,
                FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim(),
                PersonalMessage = openInvite.PersonalMessage,
                InvitedByUserId = openInvite.InvitedByUserId,
                ExpertGroupId = openInvite.ExpertGroupId,
                Token = Guid.NewGuid().ToString("N"),
                SentAt = DateTime.UtcNow,
                ExpiresAt = openInvite.ExpiresAt,
                Status = TeacherApplicationInviteStatus.Registered,
                AcceptedTenantId = tenant.Id,
                AcceptedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            return;
        }

        var teacherName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(teacherName))
            teacherName = tenant.Name;

        await _expertNotify.NotifyGroupInviteLinkSignupAsync(
            tenant.Id,
            openInvite.Id,
            teacherUserId,
            normalized,
            teacherName,
            ct);
    }

    private async Task<RegisterSchoolResponse> UpdateExistingTeacherFromInviteAsync(
        ApplicationUser user,
        Tenant tenant,
        TeacherApplicationInvite invite,
        RegisterSchoolRequest request,
        ExpertGroup? inviteGroup,
        CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (!string.Equals(tenant.Slug, slug, StringComparison.OrdinalIgnoreCase)
            && _db.Tenants.Any(t => t.Slug == slug && t.Id != tenant.Id))
            throw new InvalidOperationException("Cette adresse est déjà utilisée par un autre profil.");

        var country = ProfileVisibility.NormalizeCode(request.Country);
        if (country.Length != 2)
            country = ProfileVisibility.NormalizeCode(inviteGroup?.CountryCode);
        if (country.Length != 2 && !string.IsNullOrWhiteSpace(tenant.Country))
            country = ProfileVisibility.NormalizeCode(tenant.Country);
        if (country.Length != 2 && inviteGroup is null)
            country = "CA";

        var timeZone = TimeZoneCatalog.Normalize(
            string.IsNullOrWhiteSpace(request.TimeZone)
                ? tenant.TimeZone
                : request.TimeZone);
        var commLanguages = TeacherCommunicationLanguages.NormalizeMany(
            request.CommunicationLanguages,
            string.IsNullOrWhiteSpace(request.PreferredLanguage) ? tenant.Language : request.PreferredLanguage);
        var language = TeacherCommunicationLanguages.Primary(commLanguages);

        GroupOffer? catalogOffer = null;
        if (request.GroupOfferId is Guid offerId && offerId != Guid.Empty)
        {
            catalogOffer = _db.GroupOffers.FirstOrDefault(o =>
                o.Id == offerId
                && o.ExpertGroupId == invite.ExpertGroupId
                && o.Status == GroupOfferStatus.Published)
                ?? throw new InvalidOperationException("Offre du groupe introuvable ou non publiée.");
        }

        var currency = catalogOffer is not null
            ? catalogOffer.Currency
            : string.IsNullOrWhiteSpace(request.InitialOffering?.Currency)
                ? tenant.Currency
                : request.InitialOffering!.Currency;

        var presentation = TeacherContactPrivacy.RedactFromPublicText(request.Presentation?.Trim());
        var privateNotes = BuildPrivateProfileNotes(request.Address, request.PostalCode, request.DateOfBirth);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = NormalizeOptionalPhone(request.Phone) ?? user.PhoneNumber;
        user.PreferredLanguage = language;
        user.TimeZone = timeZone;
        user.TenantId = tenant.Id;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        tenant.Name = string.IsNullOrWhiteSpace(request.SchoolName) ? tenant.Name : request.SchoolName.Trim();
        tenant.Slug = slug;
        tenant.Subdomain = slug;
        tenant.Description = privateNotes ?? tenant.Description;
        tenant.City = string.IsNullOrWhiteSpace(request.City) ? tenant.City : request.City.Trim();
        tenant.Country = country;
        tenant.TimeZone = timeZone;
        tenant.Currency = currency;
        tenant.Language = language;
        tenant.CommunicationLanguagesCsv = TeacherCommunicationLanguages.ToCsv(commLanguages);
        tenant.TeacherConductPolicyVersion = TeacherConductPolicy.CurrentVersion;
        tenant.TeacherConductAcceptedAt = DateTime.UtcNow;
        tenant.ExpertApprovalStatus = ExpertApprovalStatus.Pending;
        tenant.ApprovedByExpertGroupId = invite.ExpertGroupId;
        tenant.ApprovedByUserId = null;
        tenant.ExpertApprovedAt = null;
        tenant.ExpertApprovalNotes = null;
        tenant.ReviewAssignedToUserId = null;
        tenant.ReviewRequestNotes = null;
        tenant.IsPublicProfile = false;
        tenant.ExpertReviewNotifiedAt = null;
        if (string.IsNullOrWhiteSpace(invite.Email))
            tenant.ReviewPriority = 1;
        if (tenant.Status is TenantStatus.Suspended or TenantStatus.Rejected)
            tenant.Status = TenantStatus.PendingValidation;
        tenant.UpdatedAt = DateTime.UtcNow;

        var branding = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
        if (branding is null)
        {
            branding = new TenantBranding { Presentation = presentation };
            tenant.Branding = branding;
        }
        else if (!string.IsNullOrWhiteSpace(presentation))
        {
            branding.Presentation = presentation;
        }

        TeacherCommunicationLanguages.ApplyToBranding(branding, commLanguages);
        branding.Portfolio = TeacherOnboardingPortfolio.Build(
            branding.Portfolio,
            commLanguages,
            request.YearsExperience,
            request.Diplomas,
            request.Certifications);

        ReplaceTeacherAvailabilities(tenant.Id, request.Availabilities, request.InitialOffering?.Schedule);
        await MarkInviteAcceptedIfAnyAsync(request.Email, tenant.Id, request.InviteToken, ct);
        await RecordOpenInviteSignupAsync(invite, tenant, user.Id, request.Email, request.FirstName, request.LastName, ct);
        await _db.SaveChangesAsync(ct);

        var offering = await TryCreateInitialOfferingAsync(
            tenant.Id,
            catalogOffer,
            request.InitialOffering,
            currency,
            timeZone,
            ct);

        if (catalogOffer is not null)
        {
            var alreadyLinked = _db.GroupOfferTeachers.Any(x =>
                x.GroupOfferId == catalogOffer.Id && x.TeacherTenantId == tenant.Id);
            if (!alreadyLinked && offering is not null)
            {
                _db.Add(new GroupOfferTeacher
                {
                    GroupOfferId = catalogOffer.Id,
                    TeacherTenantId = tenant.Id,
                    AssignmentStatus = GroupOfferTeacherAssignmentStatus.Applied,
                    TeacherPrice = offering.Price,
                    SubscriptionOfferingId = offering.Id
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        await _expertNotify.NotifyExpertsIfNeededAsync(tenant.Id, ct);
        return new RegisterSchoolResponse(tenant.Id, tenant.Slug, user.Email!);
    }

    public async Task<IssuedTeacherLogin?> IssueTemporaryPasswordAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return null;

        var password = TeacherLoginProvisioning.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, token, password);
        if (!reset.Succeeded)
            throw new InvalidOperationException(string.Join("; ", reset.Errors.Select(e => e.Description)));

        user.EmailConfirmed = true;
        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);
        return new IssuedTeacherLogin(user.Email, password);
    }

    public async Task<RegisterTeacherByExpertResponse> RegisterTeacherByExpertAsync(
        string expertUserId,
        RegisterTeacherByExpertRequest request,
        CancellationToken ct = default,
        Guid? actAsExpertGroupId = null)
    {
        ExpertGroup group;
        if (actAsExpertGroupId is Guid forcedGroupId)
        {
            group = _db.ExpertGroups.FirstOrDefault(g => g.Id == forcedGroupId && g.IsActive)
                ?? throw new InvalidOperationException("Groupe d'experts introuvable ou inactif.");
        }
        else
        {
            var membership = _db.ExpertGroupMembers
                .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
                .Select(m => m.ExpertGroupId)
                .FirstOrDefault();
            if (membership == Guid.Empty)
                throw new InvalidOperationException("Vous n'êtes membre d'aucun groupe d'experts.");

            group = _db.ExpertGroups.FirstOrDefault(g => g.Id == membership && g.IsActive)
                ?? throw new InvalidOperationException("Groupe d'experts introuvable ou inactif.");
        }

        var groupMailbox = await ResolveGroupMailboxAsync(group, ct)
            ?? throw new InvalidOperationException(
                "Le groupe n'a pas d'e-mail de contact. Définissez l'e-mail du Responsable / du groupe avant de créer un enseignant.");

        var realEmail = TeacherLoginProvisioning.TryNormalizeOptionalEmail(request.Email);
        var loginEmail = await TeacherLoginProvisioning.AllocateUniqueLoginEmailAsync(
            groupMailbox,
            async candidate => await _userManager.FindByEmailAsync(candidate) is not null,
            ct);

        var firstName = (request.FirstName ?? "").Trim();
        var lastName = (request.LastName ?? "").Trim();
        var schoolName = (request.SchoolName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(schoolName))
            schoolName = group.Name;
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new InvalidOperationException("Prénom et nom requis.");
        if (!request.AcceptedTeacherConductPolicy)
            throw new InvalidOperationException(
                "Vous devez confirmer l'acceptation du Code de conduite enseignant pour créer le compte.");

        var password = string.IsNullOrWhiteSpace(request.Password)
            ? TeacherLoginProvisioning.GenerateTemporaryPassword()
            : request.Password;
        if (password.Length < 8 || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
            password = TeacherLoginProvisioning.GenerateTemporaryPassword();

        // Pays de résidence de l'enseignant : celui transmis par le formulaire, le pays du groupe
        // à défaut. Un groupe national peut recruter un enseignant installé ailleurs, et un groupe
        // international n'a par nature aucun pays à imposer.
        var country = ProfileVisibility.NormalizeCode(request.Country);
        if (country.Length != 2)
        {
            country = ProfileVisibility.NormalizeCode(group.CountryCode);
            if (country.Length != 2)
                country = "";
        }

        var visibleCsv = ProfileVisibility.ToCsv(request.VisibleCountryCodes, country);

        AddressCoherence.ThrowIfInvalid(country, request.City, postalCode: null, street: null, phone: null);

        var requestedSlug = (request.Slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(requestedSlug))
            throw new InvalidOperationException("Sous-domaine requis.");
        if (!Regex.IsMatch(requestedSlug, @"^[a-z0-9]([a-z0-9-]{1,48}[a-z0-9])?$"))
            throw new InvalidOperationException("Sous-domaine invalide.");
        if (_db.Tenants.Any(t => t.Slug == requestedSlug))
            throw new InvalidOperationException("Cette adresse est déjà utilisée par un autre profil.");
        var slug = requestedSlug;

        var commLanguages = TeacherCommunicationLanguages.NormalizeMany(
            request.CommunicationLanguages, request.Language);
        var primaryLanguage = TeacherCommunicationLanguages.Primary(commLanguages);

        var user = new ApplicationUser
        {
            UserName = loginEmail,
            Email = loginEmail,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            MustChangePassword = true,
            PreferredLanguage = primaryLanguage
        };

        var create = await _userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, UserRoles.Tutor);

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Name = schoolName,
            Slug = slug,
            Subdomain = slug,
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            Country = country,
            VisibleCountryCodes = visibleCsv,
            Status = TenantStatus.PendingValidation,
            Plan = TenantPlan.Starter,
            OwnerUserId = user.Id,
            TimeZone = TimeZoneCatalog.Normalize(request.TimeZone),
            Currency = string.IsNullOrWhiteSpace(request.InitialOffering?.Currency)
                ? GroupOfferCurrencyRules.ResolveCurrency(country)
                : request.InitialOffering!.Currency.Trim(),
            Language = primaryLanguage,
            CommunicationLanguagesCsv = TeacherCommunicationLanguages.ToCsv(commLanguages),
            Branding = new TenantBranding
            {
                Portfolio = TeacherCommunicationLanguages.MergePortfolioLanguages(null, commLanguages)
            },
            ExpertApprovalStatus = ExpertApprovalStatus.Approved,
            ApprovedByExpertGroupId = group.Id,
            ApprovedByUserId = expertUserId,
            ExpertApprovedAt = now,
            ExpertApprovalNotes = "Compte créé et approuvé par un expert du groupe.",
            TeacherConductPolicyVersion = TeacherConductPolicy.CurrentVersion,
            TeacherConductAcceptedAt = now
        };

        _db.Add(tenant);

        _db.Add(new TeacherApplicationInvite
        {
            Email = realEmail ?? loginEmail,
            FirstName = firstName,
            InvitedByUserId = expertUserId,
            ExpertGroupId = group.Id,
            Token = Guid.NewGuid().ToString("N"),
            SentAt = now,
            AcceptedAt = now,
            AcceptedTenantId = tenant.Id,
            Status = TeacherApplicationInviteStatus.Approved,
            ExpiresAt = now.AddDays(30)
        });

        await _db.SaveChangesAsync(ct);

        user.TenantId = tenant.Id;
        user.TimeZone = tenant.TimeZone;
        await _userManager.UpdateAsync(user);

        SaveTeacherAvailabilities(tenant.Id, request.Availabilities, request.InitialOffering?.Schedule);
        await _db.SaveChangesAsync(ct);

        Guid? offeringId = null;
        if (request.InitialOffering is { } offerReq && !string.IsNullOrWhiteSpace(offerReq.Title))
        {
            var offering = await _offerings.CreateForTenantAsync(tenant.Id, offerReq, ct);
            offeringId = offering.Id;
        }

        string? publicPath = null;
        if (request.PublishPublicProfile)
        {
            var nowPub = DateTime.UtcNow;
            tenant.Status = TenantStatus.Active;
            tenant.IsPublicProfile = true;
            tenant.OnboardingCompletedAt ??= nowPub;
            if (tenant.LicenseExpiresAt is null || tenant.LicenseExpiresAt <= nowPub)
                tenant.LicenseExpiresAt = nowPub.AddYears(1);
            if (string.IsNullOrWhiteSpace(tenant.VisibleCountryCodes))
                tenant.VisibleCountryCodes = ProfileVisibility.ToCsv(null, tenant.Country);
            tenant.UpdatedAt = nowPub;
            await _db.SaveChangesAsync(ct);
            publicPath = $"/profil/{tenant.Slug}";
        }

        var loginUrl = $"{_urls.WebBaseUrl.TrimEnd('/')}/login/tuteur";
        var platformOps = _configuration["Support:OpsEmail"]
            ?? _configuration["Support:Email"]
            ?? TeacherLoginProvisioning.DefaultPlatformOpsEmail;
        var groupAdminEmail = await ResolveGroupAdminNotificationEmailAsync(group, ct) ?? groupMailbox;
        var recipients = TeacherLoginProvisioning.ResolveCredentialRecipients(
            realEmail, groupAdminEmail, platformOps);

        var anySent = false;
        foreach (var to in recipients)
        {
            try
            {
                await _email.SendTeacherAccountCredentialsAsync(
                    to, firstName, loginEmail, password, loginUrl, group.Name, ct);
                anySent = true;
            }
            catch
            {
                // Continuer les autres destinataires.
            }
        }

        return new RegisterTeacherByExpertResponse(
            tenant.Id, tenant.Slug, loginEmail, anySent, offeringId, password, realEmail,
            tenant.IsPublicProfile, publicPath);
    }

    private async Task<string?> ResolveGroupMailboxAsync(ExpertGroup group, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(group.ContactEmail) && group.ContactEmail.Contains('@'))
            return group.ContactEmail.Trim().ToLowerInvariant();

        return await ResolveGroupAdminNotificationEmailAsync(group, ct);
    }

    private async Task<string?> ResolveGroupAdminNotificationEmailAsync(ExpertGroup group, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(group.ContactEmail) && group.ContactEmail.Contains('@'))
            return group.ContactEmail.Trim().ToLowerInvariant();

        var manager = await _groupManagers.GetActiveManagerAsync(group.Id, ct);
        if (manager is null || string.IsNullOrWhiteSpace(manager.UserId))
            return null;

        var user = await _userManager.FindByIdAsync(manager.UserId);
        return string.IsNullOrWhiteSpace(user?.Email) ? null : user.Email.Trim().ToLowerInvariant();
    }

    private async Task<SubscriptionOfferingDto?> TryCreateInitialOfferingAsync(
        Guid tenantId,
        GroupOffer? catalogOffer,
        CreateSubscriptionOfferingRequest? request,
        string currency,
        string timeZone,
        CancellationToken ct)
    {
        var title = !string.IsNullOrWhiteSpace(request?.Title)
            ? request!.Title.Trim()
            : catalogOffer?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var price = request?.Price is > 0
            ? request.Price
            : catalogOffer?.RecommendedPrice ?? catalogOffer?.FixedPrice ?? 0m;
        var offerCurrency = string.IsNullOrWhiteSpace(request?.Currency)
            ? currency
            : request!.Currency.Trim();

        var schedule = request?.Schedule is { } sched
            ? sched with { TimeZone = string.IsNullOrWhiteSpace(sched.TimeZone) ? timeZone : sched.TimeZone }
            : new OfferingScheduleDto(
                "mois",
                "weekly",
                60,
                null,
                null,
                Array.Empty<OfferingScheduleSlotDto>(),
                TimeZone: timeZone);

        return await _offerings.CreateForTenantAsync(
            tenantId,
            new CreateSubscriptionOfferingRequest(
                Title: title,
                Description: request?.Description ?? catalogOffer?.ShortDescription ?? catalogOffer?.FullDescription,
                Subject: string.IsNullOrWhiteSpace(request?.Subject) ? catalogOffer?.Name : request!.Subject,
                Price: price,
                Currency: offerCurrency,
                DurationDays: request is { DurationDays: > 0 } ? request.DurationDays : 30,
                SessionCount: request is { SessionCount: > 0 } ? request.SessionCount : 4,
                Frequency: request?.Frequency,
                Mode: request?.Mode ?? "En ligne",
                Conditions: request?.Conditions,
                Schedule: schedule,
                MaxCapacity: request is { MaxCapacity: > 0 } ? request.MaxCapacity : 20),
            ct);
    }

    private static string? NormalizeOptionalPhone(string? phone)
    {
        var value = phone?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? BuildPrivateProfileNotes(string? address, string? postal, DateTime? birthDate)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            string.IsNullOrWhiteSpace(postal) ? null : $"CP {postal.Trim()}",
            birthDate is DateTime d ? $"Né(e) le {d:dd/MM/yyyy}" : null
        }.Where(x => !string.IsNullOrWhiteSpace(x));
        var notes = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
    }

    private void ReplaceTeacherAvailabilities(
        Guid tenantId,
        IReadOnlyList<TeacherAvailabilityRangeDto>? availabilities,
        OfferingScheduleDto? schedule = null) =>
        SaveTeacherAvailabilities(tenantId, availabilities, schedule);

    private void SaveTeacherAvailabilities(
        Guid tenantId,
        IReadOnlyList<TeacherAvailabilityRangeDto>? availabilities,
        OfferingScheduleDto? schedule = null)
    {
        var previous = _db.TeacherAvailabilitiesForAnyTenant.Where(a => a.TenantId == tenantId).ToList();
        if (previous.Count > 0)
            _db.RemoveRange(previous);
        var ranges = availabilities?
            .Where(a => !string.IsNullOrWhiteSpace(a.Day)
                        && !string.IsNullOrWhiteSpace(a.StartTime)
                        && !string.IsNullOrWhiteSpace(a.EndTime))
            .ToList() ?? [];

        if (ranges.Count == 0 && schedule?.Slots is { Count: > 0 } slots)
        {
            ranges = slots
                .Where(s => !string.IsNullOrWhiteSpace(s.Day) && !string.IsNullOrWhiteSpace(s.Time))
                .Select(s => new TeacherAvailabilityRangeDto(
                    s.Day,
                    s.Time,
                    string.IsNullOrWhiteSpace(s.EndTime) ? s.Time : s.EndTime!))
                .ToList();
        }

        foreach (var range in ranges)
        {
            if (!AvailabilityWindows.TryParseDay(range.Day, out var day))
                continue;
            if (!AvailabilityWindows.TryParseTime(range.StartTime, out var start)
                || !AvailabilityWindows.TryParseTime(range.EndTime, out var end)
                || end <= start)
                continue;

            _db.Add(new TeacherAvailability
            {
                TenantId = tenantId,
                DayOfWeek = day,
                StartTime = TimeOnly.FromTimeSpan(start),
                EndTime = TimeOnly.FromTimeSpan(end),
                IsActive = true
            });
        }
    }

    private string AllocateUniqueSlug(string schoolName)
    {
        var baseSlug = Regex.Replace(schoolName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "enseignant";
        if (baseSlug.Length > 40)
            baseSlug = baseSlug[..40].Trim('-');

        for (var i = 0; i < 20; i++)
        {
            var candidate = i == 0
                ? baseSlug
                : $"{baseSlug}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
            if (!_db.Tenants.Any(t => t.Slug == candidate))
                return candidate;
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(48, baseSlug.Length + 9)];
    }

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

        for (var i = code.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (code[i], code[j]) = (code[j], code[i]);
        }

        return new string(code);
    }

    public async Task<TeacherInviteInfoResponse?> GetTeacherInviteInfoAsync(string token, CancellationToken ct = default)
    {
        var trimmed = (token ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var invite = _db.TeacherApplicationInvites.FirstOrDefault(i => i.Token == trimmed);
        if (invite is null)
            return null;

        if (invite.Status is TeacherApplicationInviteStatus.Expired
            or TeacherApplicationInviteStatus.Rejected
            or TeacherApplicationInviteStatus.Approved
            or TeacherApplicationInviteStatus.Registered)
            return null;

        if (invite.ExpiresAt is DateTime exp && exp < DateTime.UtcNow)
        {
            invite.Status = TeacherApplicationInviteStatus.Expired;
            invite.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var group = _db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId);
        if (group is null)
            return null;

        var memberCount = _db.ExpertGroupMembers.Count(m =>
            m.ExpertGroupId == group.Id && m.Status == ExpertMembershipStatus.Active);
        var offers = _db.GroupOffers
            .Where(o => o.ExpertGroupId == group.Id && o.Status == GroupOfferStatus.Published)
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.ShortDescription,
                o.Currency,
                Price = o.RecommendedPrice ?? o.FixedPrice,
                o.IsInternational,
                o.Code,
                o.MarketCountryCode,
                o.LevelsCsv
            })
            .ToList()
            .Select(o => new TeacherInvitePublicOfferDto(
                o.Id,
                o.Name,
                o.ShortDescription,
                o.Currency,
                o.Price,
                o.IsInternational,
                o.Code,
                o.MarketCountryCode,
                SchoolLevelCatalog.ParseLevelCsv(o.LevelsCsv)))
            .ToList();

        var isProfileUpdate = false;
        string? existingSlug = null;
        var firstName = invite.FirstName;
        string? lastName = null;
        string? city = null;
        string? presentation = null;
        string? timeZone = null;

        if (!string.IsNullOrWhiteSpace(invite.Email))
        {
            var existingUser = await _userManager.FindByEmailAsync(invite.Email.Trim());
            if (existingUser is not null)
            {
                var roles = await _userManager.GetRolesAsync(existingUser);
                if (roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant))
                {
                    var existingTenant = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == existingUser.Id)
                        ?? (existingUser.TenantId is Guid tid
                            ? _db.Tenants.FirstOrDefault(t => t.Id == tid)
                            : null);
                    if (existingTenant is not null)
                    {
                        isProfileUpdate = true;
                        existingSlug = existingTenant.Slug;
                        firstName = string.IsNullOrWhiteSpace(existingUser.FirstName)
                            ? invite.FirstName
                            : existingUser.FirstName;
                        lastName = existingUser.LastName;
                        city = existingTenant.City;
                        timeZone = existingTenant.TimeZone;
                        presentation = _db.TenantBrandings.FirstOrDefault(b => b.TenantId == existingTenant.Id)?.Presentation;
                    }
                }
            }
        }

        return new TeacherInviteInfoResponse(
            group.Id,
            group.Name,
            string.IsNullOrWhiteSpace(invite.Email) ? null : invite.Email,
            firstName,
            invite.PersonalMessage,
            await ResolveInviterGivenNameAsync(invite.InvitedByUserId, group.ContactName),
            group.Description,
            group.LogoUrl,
            group.CountryCode,
            group.IsInternational,
            memberCount,
            invite.ExpiresAt,
            offers,
            IsProfileUpdate: isProfileUpdate,
            ExistingSlug: existingSlug,
            LastName: lastName,
            City: city,
            Presentation: presentation,
            TimeZone: timeZone);
    }

    private async Task<string?> ResolveInviterGivenNameAsync(string invitedByUserId, string? groupContactName)
    {
        if (!string.IsNullOrWhiteSpace(invitedByUserId))
        {
            var inviter = await _userManager.FindByIdAsync(invitedByUserId);
            if (inviter is not null)
            {
                var given = TeacherPublicName.GivenNameOnly(inviter.FirstName, inviter.LastName);
                if (given.Length > 0)
                    return given;
            }
        }

        var fromContact = TeacherPublicName.GivenNameFromDisplay(groupContactName);
        return string.IsNullOrWhiteSpace(fromContact) ? null : fromContact;
    }

    private async Task MarkInviteAcceptedIfAnyAsync(
        string email,
        Guid tenantId,
        string? inviteToken,
        CancellationToken ct)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        TeacherApplicationInvite? invite = null;

        if (!string.IsNullOrWhiteSpace(inviteToken))
        {
            invite = _db.TeacherApplicationInvites
                .FirstOrDefault(i => i.Token == inviteToken.Trim());
        }

        if (invite is null && !string.IsNullOrWhiteSpace(normalized))
        {
            invite = _db.TeacherApplicationInvites
                .Where(i => i.Email == normalized
                            && i.Status == TeacherApplicationInviteStatus.Sent)
                .OrderByDescending(i => i.SentAt)
                .FirstOrDefault();
        }

        if (invite is null)
            return;

        if (string.IsNullOrWhiteSpace(invite.Email))
            return;

        invite.AcceptedTenantId = tenantId;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.Status = TeacherApplicationInviteStatus.Registered;
        invite.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            throw new InvalidOperationException("Le lien de confirmation est invalide ou expiré.");
    }

    public async Task ResendEmailConfirmationAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var user = await _userManager.FindByEmailAsync(normalized);
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
            return;

        var key = normalized.ToLowerInvariant();
        if (ConfirmResendCooldown.TryGetValue(key, out var last)
            && DateTime.UtcNow - last < ConfirmResendMinInterval)
            return;

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var isParent = roles.Any(r => r.Equals(UserRoles.Parent, StringComparison.OrdinalIgnoreCase));
        var isTutor = roles.Any(r =>
            r.Equals(UserRoles.Tutor, StringComparison.OrdinalIgnoreCase)
            || r.Equals(UserRoles.TeachingAssistant, StringComparison.OrdinalIgnoreCase));

        if (isParent)
        {
            var parentConfirmUrl = _urls.BuildEmailConfirmUrl(user.Id, token, "/login/parent?confirmed=true");
            await _email.SendParentAccessConfirmationAsync(user.Email!, user.FirstName, parentConfirmUrl, ct);
        }
        else if (isTutor)
        {
            var confirmUrl = _urls.BuildEmailConfirmUrl(user.Id, token);
            await _email.SendEmailConfirmationAsync(user.Email!, user.FirstName, confirmUrl, ct);
        }
        else
        {
            var confirmUrl = _urls.BuildEmailConfirmUrl(user.Id, token);
            await _email.SendEmailConfirmationSimpleAsync(user.Email!, user.FirstName, confirmUrl, ct);
        }

        ConfirmResendCooldown[key] = DateTime.UtcNow;
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return; // silent — don't reveal whether email exists

        if (user.TenantId is Guid tenantId)
        {
            var tenant = _db.Tenants.FirstOrDefault(t => t.Id == tenantId);
            if (tenant is not null
                && tenant.ExpertApprovalStatus is not ExpertApprovalStatus.Approved)
                return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = $"{_urls.WebBaseUrl}/reset-password?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
        await _email.SendResetPasswordAsync(user.Email!, user.FirstName, resetUrl, ct);
    }

    public async Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        await _email.SendPasswordChangedAsync(user.Email!, user.FirstName, ct);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            throw new InvalidOperationException("Mot de passe actuel et nouveau mot de passe requis.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
            await _email.SendPasswordChangedAsync(user.Email, user.FirstName, ct);

        var roles = await _userManager.GetRolesAsync(user);
        var role = ResolvePrimaryRole(roles);
        return await BuildAuthResponse(user, role, roles);
    }

    private async Task<AuthResponse> BuildAuthResponse(
        ApplicationUser user,
        string role,
        IList<string>? allRoles = null)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var expires = DateTime.UtcNow.AddHours(double.Parse(jwtSection["ExpireHours"] ?? "24"));

        string? tenantName = null;
        // Les admins plateforme n'ont pas d'école / branding tuteur dans le JWT.
        var isPlatformAdmin = role is UserRoles.SuperAdmin or UserRoles.PlatformAdmin;
        if (!isPlatformAdmin && user.TenantId.HasValue)
        {
            tenantName = _db.Tenants
                .Where(t => t.Id == user.TenantId.Value)
                .Select(t => t.Name)
                .FirstOrDefault();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role)
        };

        // Toutes les rôles Identity (AuthorizeView / IsInRole).
        if (allRoles is not null)
        {
            foreach (var r in allRoles.Where(r => !string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
                claims.Add(new Claim(ClaimTypes.Role, r));
        }

        if (!isPlatformAdmin && user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        if (!isPlatformAdmin && !string.IsNullOrWhiteSpace(tenantName))
            claims.Add(new Claim("tenant_name", tenantName));

        if (user.MustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));

        // Élève rattaché à un parent (connexion code parent inclus) : recherche enseignant = espace parent uniquement.
        if (string.Equals(role, UserRoles.Student, StringComparison.OrdinalIgnoreCase)
            && _db.StudentsForAnyTenant.Any(s => s.UserId == user.Id && s.ParentProfileId != null))
        {
            claims.Add(new Claim("parent_managed", "true"));
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResponse(
            tokenString,
            user.Email ?? string.Empty,
            user.FullName,
            role,
            isPlatformAdmin ? null : user.TenantId,
            expires,
            isPlatformAdmin ? null : tenantName,
            user.MustChangePassword);
    }

    /// <summary>Priorité : SuperAdmin → PlatformAdmin → GroupManager → Expert → premier autre rôle.</summary>
    private static string ResolvePrimaryRole(IList<string> roles)
    {
        if (roles.Contains(UserRoles.SuperAdmin))
            return UserRoles.SuperAdmin;
        if (roles.Contains(UserRoles.PlatformAdmin))
            return UserRoles.PlatformAdmin;
        if (roles.Contains(UserRoles.GroupManager))
            return UserRoles.GroupManager;
        if (roles.Contains(UserRoles.Expert))
            return UserRoles.Expert;
        return roles.FirstOrDefault() ?? UserRoles.Parent;
    }

    private static string NormalizeRole(string role) =>
        UserRoles.All.FirstOrDefault(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
        ?? UserRoles.Parent;

    /// <summary>Un expert ne peut se connecter que s'il a une adhésion Active à un groupe actif.</summary>
    private void EnsureExpertGroupIsActive(string userId)
    {
        var activeMembership = _db.ExpertGroupMembers
            .Where(m => m.UserId == userId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToList();

        if (activeMembership.Count == 0)
            throw new UnauthorizedAccessException(
                "Votre adhésion au groupe d'experts n'est pas active. Contactez l'administrateur.");

        var hasActiveGroup = _db.ExpertGroups.Any(g => activeMembership.Contains(g.Id) && g.IsActive);
        if (!hasActiveGroup)
            throw new UnauthorizedAccessException(
                "Votre groupe d'experts a été désactivé. Contactez l'administrateur de la plateforme.");
    }

    private async Task EnsureParentProfileAsync(ApplicationUser user, CancellationToken ct, string? country = null)
    {
        var iso = FamilyResidence.TryIso(country);
        var existing = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == user.Id);
        if (existing is not null)
        {
            if (iso is not null && string.IsNullOrWhiteSpace(existing.Country))
            {
                existing.Country = iso;
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        // Prefers the user's school; otherwise the dedicated holding tenant for marketplace parents
        // (avoids attaching every parent to Tenants.First() / a random school).
        var tenantId = user.TenantId
            ?? await EnsureMarketplaceParentTenantIdAsync(ct);
        if (tenantId == Guid.Empty)
            return;

        _db.Add(new ParentProfile
        {
            TenantId = tenantId,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? user.UserName ?? string.Empty,
            Country = iso
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Guid> EnsureMarketplaceParentTenantIdAsync(CancellationToken ct)
    {
        const string slug = "platform-parents";
        var existing = _db.Tenants.Where(t => t.Slug == slug).Select(t => t.Id).FirstOrDefault();
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
        _db.Add(holding);
        await _db.SaveChangesAsync(ct);
        return holding.Id;
    }

    private static DateTime ValidateStudentRegistrationDob(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            throw new InvalidOperationException("La date de naissance est obligatoire pour un compte élève.");

        var dob = dateOfBirth.Value.Date;
        if (dob > DateTime.UtcNow.Date)
            throw new InvalidOperationException("La date de naissance ne peut pas être dans le futur.");

        var age = (int)((DateTime.Today - dob).TotalDays / 365.25);
        if (age < 14)
            throw new InvalidOperationException(
                "L'inscription autonome est réservée aux élèves de 14 ans et plus. Demandez à un parent de créer votre compte.");

        return DateTime.SpecifyKind(dob, DateTimeKind.Utc);
    }

    private async Task EnsureStudentProfileOnRegisterAsync(
        ApplicationUser user,
        DateTime? dateOfBirth,
        CancellationToken ct)
    {
        if (_db.StudentsForAnyTenant.Any(s => s.UserId == user.Id))
            return;

        var dob = dateOfBirth
            ?? throw new InvalidOperationException("La date de naissance est obligatoire pour un compte élève.");

        // Élève autonome : rattachement à l'école du compte si déjà connue, sinon holding marketplace.
        var tenantId = user.TenantId ?? await EnsureMarketplaceParentTenantIdAsync(ct);
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                "Aucun profil disponible pour finaliser l'inscription. Réessayez plus tard.");

        var billingParent = new ParentProfile
        {
            TenantId = tenantId,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? user.UserName ?? string.Empty
        };
        _db.Add(billingParent);
        await _db.SaveChangesAsync(ct);

        _db.Add(new Student
        {
            TenantId = tenantId,
            UserId = user.Id,
            ParentProfileId = billingParent.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            DateOfBirth = dob,
            IsActive = true
        });
        await _db.SaveChangesAsync(ct);
    }
}
