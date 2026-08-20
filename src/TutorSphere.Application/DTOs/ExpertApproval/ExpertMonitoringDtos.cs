using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record MonitoredTeacherDto(
    Guid TenantId,
    string SchoolName,
    string? Country,
    string? City,
    string? OwnerEmail,
    string? OwnerName,
    int TotalLessons,
    int CancelledLessons,
    int NoShowIncidents,
    DateTime? LastActivityAt,
    int RemarkCount,
    DateTime? LastRemarkAt);

/// <summary>Ligne du répertoire enseignants du groupe (tous statuts pertinents).</summary>
public record TeacherDirectoryItemDto(
    Guid TenantId,
    string DisplayName,
    string? OwnerEmail,
    string? LogoUrl,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Levels,
    int? YearsExperience,
    ExpertApprovalStatus ApprovalStatus,
    DateTime CreatedAt,
    string Slug,
    bool IsPublicProfile,
    string? City,
    string? Country,
    bool JoinedViaInviteLink = false);

/// <summary>Support de cours unifié (devoir ou document) pour la revue qualité par un expert.</summary>
public record TeacherMaterialItemDto(
    Guid Id,
    string Kind,
    string Title,
    string? Subject,
    string? FileUrl,
    DateTime CreatedAt,
    int RemarkCount);

public record ExpertRemarkDto(
    Guid Id,
    Guid TenantId,
    ExpertRemarkCategory Category,
    string Message,
    Guid? RelatedHomeworkId,
    Guid? RelatedDocumentId,
    string AuthorUserId,
    string? AuthorName,
    DateTime CreatedAt,
    DateTime? ReadByTeacherAt);

public record CreateExpertRemarkRequest(
    ExpertRemarkCategory Category,
    string Message,
    Guid? RelatedHomeworkId = null,
    Guid? RelatedDocumentId = null);
