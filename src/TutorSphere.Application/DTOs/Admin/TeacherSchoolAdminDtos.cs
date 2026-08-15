namespace TutorSphere.Application.DTOs.Admin;

/// <summary>Fiche école / enseignant éditable par Admin ou Expert du groupe.</summary>
public record TeacherSchoolRecordDto(
    Guid TenantId,
    string OwnerUserId,
    string OwnerEmail,
    string FirstName,
    string LastName,
    string? Phone,
    string SchoolName,
    string Slug,
    string? Description,
    /// <summary>Texte d'accueil affiché sur /profil/{slug} (TenantBranding.Presentation).</summary>
    string? Presentation,
    string? City,
    string? Country,
    string Language,
    string Currency,
    IReadOnlyList<string> VisibleCountryCodes,
    bool IsPublicProfile,
    bool IsActiveSchool,
    bool HasValidLicense,
    bool OnboardingCompleted,
    DateTime? LicenseExpiresAt,
    int ExpertApprovalStatus,
    Guid? ApprovedByExpertGroupId,
    string? ApprovedByExpertGroupName,
    decimal LicenseFeeWithholdingRemainingUsd = 0m);

public record UpdateTeacherSchoolRecordRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? SchoolName = null,
    string? Description = null,
    string? Presentation = null,
    string? City = null,
    string? Country = null,
    string? Language = null,
    string? Currency = null,
    IReadOnlyList<string>? VisibleCountryCodes = null,
    /// <summary>Si true, publie la fiche après enregistrement (admin / expert).</summary>
    bool? Publish = null);

public record PublishTeacherPublicProfileResult(
    Guid TenantId,
    string Slug,
    bool IsPublicProfile,
    string PublicPath);

/// <summary>Élève actuellement inscrit à une offre d'un enseignant (abonnement actif).</summary>
public record AdminTeacherActiveStudentDto(
    Guid SubscriptionId,
    Guid StudentId,
    string StudentName,
    string? StudentEmail,
    string? ParentName,
    Guid OfferingId,
    string OfferingTitle,
    string? Subject,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    int SessionsRemaining,
    decimal Price,
    string Currency);
