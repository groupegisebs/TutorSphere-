namespace TutorSphere.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role,
    DateTime? DateOfBirth = null,
    string? PreferredLanguage = null);

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
    bool AcceptedTeacherConductPolicy = false,
    string? TeacherConductPolicyVersion = null,
    string? InviteToken = null);

public record RegisterSchoolResponse(
    Guid TenantId,
    string TenantSlug,
    string Email);

/// <summary>Infos publiques d'une invitation enseignant, utilisées pour préremplir le formulaire d'inscription.</summary>
public record TeacherInviteInfoResponse(
    Guid ExpertGroupId,
    string ExpertGroupName,
    string? Email,
    string? FirstName);

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
