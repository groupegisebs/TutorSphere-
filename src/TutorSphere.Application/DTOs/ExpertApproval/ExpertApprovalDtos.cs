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
    DateTime CreatedAt);

public record ExpertGroupMemberDto(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    bool AccountCreated = false,
    bool CredentialsSent = false,
    bool NotificationSent = false);

public record CreateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    string? LogoUrl = null,
    string? ContactName = null);

public record UpdateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LogoUrl,
    bool IsActive,
    string? ContactName = null);

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
    string SchoolName,
    string? City = null,
    string? Country = null,
    IReadOnlyList<string>? VisibleCountryCodes = null);

public record RegisterTeacherByExpertResponse(
    Guid TenantId,
    string TenantSlug,
    string Email,
    bool CredentialsSent);

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
