using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.DTOs.SubscriptionOfferings;

namespace TutorSphere.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role,
    DateTime? DateOfBirth = null,
    string? PreferredLanguage = null,
    /// <summary>Code parrainage parent (optionnel), ex. FAM-AB12CD.</summary>
    string? ReferralCode = null,
    /// <summary>Pays ISO du parent (obligatoire pour le rôle Parent) — moyens de paiement et pays de l'enfant.</summary>
    string? Country = null);

public record RegisterSchoolRequest(
    string SchoolName,
    string Slug,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? City = null,
    string? Country = null,
    string? PreferredLanguage = null,
    /// <summary>Langues de communication (plusieurs choix). La première sert aux contrats et à l'interface.</summary>
    IReadOnlyList<string>? CommunicationLanguages = null,
    bool AcceptedTeacherConductPolicy = false,
    string? TeacherConductPolicyVersion = null,
    string? InviteToken = null,
    string? Phone = null,
    string? Address = null,
    string? PostalCode = null,
    DateTime? DateOfBirth = null,
    string? Presentation = null,
    string? TimeZone = null,
    IReadOnlyList<TeacherAvailabilityRangeDto>? Availabilities = null,
    CreateSubscriptionOfferingRequest? InitialOffering = null,
    Guid? GroupOfferId = null,
    string? PhotoBase64 = null,
    string? PhotoContentType = null,
    int YearsExperience = 0,
    IReadOnlyList<PublicCredentialDto>? Diplomas = null,
    IReadOnlyList<PublicCredentialDto>? Certifications = null);

public record RegisterSchoolResponse(
    Guid TenantId,
    string TenantSlug,
    string Email);

/// <summary>Infos publiques d'une invitation enseignant, utilisées pour préremplir le formulaire d'inscription.</summary>
public record TeacherInvitePublicOfferDto(
    Guid Id,
    string Name,
    string? ShortDescription,
    string Currency,
    decimal? RecommendedPrice,
    bool IsInternational,
    string? Code = null,
    string? MarketCountryCode = null,
    IReadOnlyList<string>? Levels = null);

public record TeacherInviteInfoResponse(
    Guid ExpertGroupId,
    string ExpertGroupName,
    string? Email,
    string? FirstName,
    string? PersonalMessage = null,
    string? InviterName = null,
    string? GroupDescription = null,
    string? GroupLogoUrl = null,
    string? GroupCountryCode = null,
    bool GroupIsInternational = false,
    int GroupMemberCount = 0,
    DateTime? ExpiresAt = null,
    IReadOnlyList<TeacherInvitePublicOfferDto>? Offers = null,
    bool IsProfileUpdate = false,
    string? ExistingSlug = null,
    string? LastName = null,
    string? City = null,
    string? Presentation = null,
    string? TimeZone = null);

public record LoginRequest(string Email, string Password);

/// <summary>Connexion élève sans e-mail propre : e-mail du parent + code généré.</summary>
public record ChildLoginRequest(string ParentEmail, string AccessCode);

public record ChildLoginAccessDto(
    Guid StudentId,
    bool HasLoginAccess,
    string? AccessCode,
    string? LoginHint);

public record AuthResponse(
    string Token,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    DateTime ExpiresAt,
    string? TenantName = null,
    bool MustChangePassword = false);

public record ForgotPasswordRequest(string Email);

public record ResendEmailConfirmationRequest(string Email);

public record ConfirmEmailRequest(string UserId, string Token);

public record ResetPasswordRequest(string UserId, string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
