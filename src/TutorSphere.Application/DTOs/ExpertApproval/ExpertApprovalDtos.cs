using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record ExpertGroupDto(
    Guid Id,
    string Name,
    string? LogoUrl,
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
    string? LogoUrl = null);

public record UpdateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LogoUrl,
    bool IsActive);

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

public record TeacherApprovalStatusDto(
    ExpertApprovalStatus Status,
    string? Notes,
    DateTime? ApprovedAt,
    Guid? ExpertGroupId,
    string? ExpertGroupName,
    string? ExpertGroupLogoUrl);
