using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record ExpertGroupDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    string? Description = null,
    ExpertGroupLifecycleStatus LifecycleStatus = ExpertGroupLifecycleStatus.Draft,
    Guid? ActiveManagerMandateId = null,
    string? ManagerFullName = null,
    string? ManagerEmail = null,
    string? ManagerPhone = null,
    string? ManagerUserId = null,
    bool CanHardDelete = true);

public record ExpertGroupMemberDto(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    bool AccountCreated = false,
    bool CredentialsSent = false,
    bool NotificationSent = false,
    ExpertGroupMemberRole MemberRole = ExpertGroupMemberRole.Expert,
    ExpertMembershipStatus Status = ExpertMembershipStatus.Active,
    string? Specialty = null);

public record CreateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    string? LogoUrl = null,
    string? ContactName = null,
    string? Description = null,
    /// <summary>Responsable obligatoire à la création (utilisateur Expert existant).</summary>
    string? ManagerUserId = null,
    string? ManagerEmail = null,
    string? ManagerFirstName = null,
    string? ManagerLastName = null,
    string? ManagerPhone = null,
    string? ManagerFunctionTitle = null,
    DateTime? ManagerMandateStartsAtUtc = null,
    bool CreateManagerAccount = true);

public record UpdateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LogoUrl,
    bool IsActive,
    string? ContactName = null,
    string? Description = null,
    string? CountryCode = null);

public record AddExpertMemberRequest(string UserId);

/// <summary>
/// <paramref name="Invite"/> = true crée le compte si besoin et envoie un mot de passe temporaire.
/// <paramref name="Invite"/> = false ajoute uniquement un compte existant (notification sans MDP).
/// </summary>
public record AddExpertByEmailRequest(
    string Email,
    bool Invite = false,
    string? FirstName = null,
    string? LastName = null);

public record TeacherDocumentDto(
    Guid Id,
    Guid TenantId,
    TeacherDocumentType DocumentType,
    string FileName,
    string FileUrl,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    string? Notes);

public record PendingTeacherDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Country,
    string? City,
    ExpertApprovalStatus ApprovalStatus,
    DateTime CreatedAt,
    string? OwnerEmail,
    string? OwnerName,
    int DocumentCount,
    Guid? AssignedExpertGroupId,
    string? AssignedExpertGroupName);

public record TeacherReviewDetailDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Description,
    string? Country,
    string? City,
    string Language,
    ExpertApprovalStatus ApprovalStatus,
    string? ExpertApprovalNotes,
    DateTime? ExpertApprovedAt,
    Guid? ApprovedByExpertGroupId,
    string? ApprovedByExpertGroupName,
    string? ApprovedByExpertGroupLogoUrl,
    string? OwnerUserId,
    string? OwnerEmail,
    string? OwnerName,
    string? Presentation,
    string? Portfolio,
    string? LogoUrl,
    IReadOnlyList<TeacherDocumentDto> Documents,
    Guid? SuggestedExpertGroupId,
    string? SuggestedExpertGroupName);

public record ExpertDecisionRequest(string? Notes);

public record InviteTeacherApplicationRequest(
    string Email,
    string? FirstName = null,
    string? PersonalMessage = null);

/// <summary>Création directe d'un compte enseignant par un expert membre d'un groupe.</summary>
public record RegisterTeacherByExpertRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? SchoolName = null,
    string? Slug = null,
    string? City = null,
    string? Country = null,
    IReadOnlyList<string>? VisibleCountryCodes = null,
    bool AcceptedTeacherConductPolicy = false);

public record RegisterTeacherByExpertResponse(
    Guid TenantId,
    string TenantSlug,
    string Email,
    bool CredentialsSent);

/// <summary>Groupe d'experts auquel l'utilisateur connecté appartient.</summary>
public record ExpertMyGroupDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? Description = null,
    bool IsInternational = false);

public record UpdateManagerGroupSettingsRequest(string? Description);

public record TeacherApplicationInviteDto(
    Guid Id,
    string Email,
    string? FirstName,
    TeacherApplicationInviteStatus Status,
    DateTime SentAt,
    DateTime? ExpiresAt,
    DateTime? AcceptedAt,
    Guid? AcceptedTenantId,
    string? InvitedByUserId,
    string? InvitedByName,
    Guid ExpertGroupId,
    string? ExpertGroupName,
    string? SchoolName);

public record TeacherApprovalStatusDto(
    ExpertApprovalStatus Status,
    string? Notes,
    DateTime? ApprovedAt,
    Guid? ExpertGroupId,
    string? ExpertGroupName,
    string? ExpertGroupLogoUrl);
