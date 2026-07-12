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
    string? LogoUrl = null,
    string? BannerUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    string? Presentation = null,
    string? Portfolio = null);

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
    string Mode,
    string? ScheduleSummary = null,
    IReadOnlyList<string>? AvailabilitySlots = null);

public record PublicTenantSiteDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? City,
    string? Country,
    TenantBrandingDto Branding,
    IReadOnlyList<PublicOfferingDto> Offerings);

public record PublicCredentialDto(string Title, string? Institution, string? Year);

public record PublicTutorDetailDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? ShortBio,
    string? City,
    string? Country,
    string Language,
    string Currency,
    bool IsPublicProfile,
    string? OwnerUserId,
    string? TutorFirstName,
    string? TutorLastName,
    string? TutorFullName,
    string? PhotoUrl,
    string? BannerUrl,
    string PrimaryColor,
    string SecondaryColor,
    string? FullBio,
    int YearsExperience,
    decimal HourlyRate,
    string? Status,
    IReadOnlyList<PublicCredentialDto> Diplomas,
    IReadOnlyList<PublicCredentialDto> Certifications,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Availability,
    IReadOnlyList<PublicOfferingDto> Offerings);
