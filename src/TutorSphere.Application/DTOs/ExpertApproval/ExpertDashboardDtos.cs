using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record ExpertDashboardAlertDto(
    string Severity,
    string Code,
    string Message,
    string? ActionUrl = null);

public record ExpertDashboardSummaryDto(
    string GroupName,
    string? CountryCode,
    int TeachersPending,
    int TeachersUnderReview,
    int TeachersChangesRequested,
    int TeachersApproved,
    int TeachersSuspended,
    int ExpertVotesOpen,
    int ExpertInvitesPending,
    int TeacherInvitesSent,
    int UnreadRemarksForTeachers,
    int CancelledLessonsRecent,
    int DisciplinesWithoutTeacher,
    int TeachersInactive,
    int ActiveGroupMembers,
    Guid? NextPendingTenantId,
    Guid? NextOpenExpertVoteId,
    IReadOnlyList<ExpertDashboardAlertDto> Alerts,
    int PendingTeacherEnrollments = 0);

public record ExpertApprovalQueueFilter(
    string? Country = null,
    string? City = null,
    ExpertApprovalStatus? Status = null,
    int? MinDocuments = null,
    bool? IncompleteOnly = null,
    bool? UrgentOnly = null,
    string? AssignedToUserId = null,
    int? OlderThanDays = null);

public record ExpertApprovalQueueItemDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Country,
    string? City,
    ExpertApprovalStatus ApprovalStatus,
    DateTime CreatedAt,
    int AgeDays,
    string? OwnerEmail,
    string? OwnerName,
    int DocumentCount,
    bool IsComplete,
    int ReviewPriority,
    string? ReviewAssignedToUserId,
    string? ReviewAssignedToName,
    string? ReviewRequestNotes);

public record TeacherDecisionItemDto(
    Guid TenantId,
    string DisplayName,
    string? OwnerEmail,
    ExpertApprovalStatus ApprovalStatus,
    DateTime? DecisionAt,
    string? Notes);

public record AssignReviewRequest(string? AssigneeUserId, bool Urgent = false);

public record RequestChangesRequest(string Notes);
