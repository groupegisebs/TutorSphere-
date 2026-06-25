namespace TutorSphere.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    DateTime ExpiresAt);
