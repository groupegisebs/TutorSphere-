using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.ExpertApproval;

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

/// <summary>
/// Fiche publique enseignant. Aucun PII : pas d’adresse, de naissance, d’e-mail, de téléphone,
/// d’identifiant interne ni de nom de famille complet.
/// </summary>
public sealed class TeacherPublicProfileDto
{
    public string Slug { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string GivenName { get; init; } = "";
    public string PublicInitials { get; init; } = "?";
    public string? Location { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string Language { get; init; } = "fr";
    public string Currency { get; init; } = "EUR";
    public string? PhotoUrl { get; init; }
    public string PhotoKind { get; init; } = "initials";
    public bool PhotoIsGroupLogoFallback { get; init; }
    public string? BannerUrl { get; init; }
    public string PrimaryColor { get; init; } = ColorHex.TutorSpherePrimary;
    public string SecondaryColor { get; init; } = ColorHex.TutorSphereSecondary;
    public string? ShortBio { get; init; }
    public string? FullBio { get; init; }
    public int YearsExperience { get; init; }
    public decimal HourlyRate { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<PublicCredentialDto> Diplomas { get; init; } = [];
    public IReadOnlyList<PublicCredentialDto> Certifications { get; init; } = [];
    public IReadOnlyList<string> Subjects { get; init; } = [];
    public IReadOnlyList<string> Levels { get; init; } = [];
    public IReadOnlyList<string> Languages { get; init; } = [];
    public IReadOnlyList<string> Availability { get; init; } = [];
    public IReadOnlyList<PublicOfferingDto> Offerings { get; init; } = [];
    public string? ExpertGroupName { get; init; }
    public string? ExpertGroupLogoUrl { get; init; }
    public string? ExpertGroupCountryCode { get; init; }
    public bool IsVerified { get; init; }
    public decimal? Rating { get; init; }
    public int ReviewCount { get; init; }
    public IReadOnlyList<PublicDisciplineDto>? Disciplines { get; init; }
}

