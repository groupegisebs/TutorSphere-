namespace TutorSphere.Application.DTOs.PlatformPromo;

public record PlatformPromoCodeDto(
    Guid Id,
    string Code,
    bool IsActive,
    int LicenseYears,
    DateTime? ExpiresAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? RedeemedAt,
    Guid? RedeemedByTenantId,
    string? RedeemedByUserId,
    string? RedeemedBySchoolName,
    bool IsAvailable);

public record CreatePlatformPromoCodeRequest(
    string? Code,
    int LicenseYears = 1,
    DateTime? ExpiresAt = null,
    string? Notes = null,
    int Quantity = 1);

public record DeactivatePlatformPromoCodeRequest(bool IsActive);

public record RedeemPlatformPromoRequest(string Code);
