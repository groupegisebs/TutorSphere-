namespace TutorSphere.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);

public record RegisterSchoolRequest(
    string SchoolName,
    string Slug,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? City = null,
    string? Country = null);

public record RegisterSchoolResponse(
    Guid TenantId,
    string TenantSlug,
    string Email);

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
    string? TenantName = null);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string UserId, string Token, string NewPassword);
