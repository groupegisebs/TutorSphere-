namespace TutorSphere.Application.DTOs.Branding;

public record TenantBrandingDto(
    Guid Id,
    Guid TenantId,
    string? LogoUrl,
    string? BannerUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? Presentation,
    string? Portfolio);

public record UpdateTenantBrandingRequest(
    string? LogoUrl,
    string? BannerUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? Presentation,
    string? Portfolio);

public record PublicOfferingDto(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    decimal Price,
    string Currency,
    int DurationDays,
    int SessionCount,
    string? Frequency,
    string Mode);

public record PublicTenantSiteDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? City,
    string? Country,
    TenantBrandingDto Branding,
    IReadOnlyList<PublicOfferingDto> Offerings);
