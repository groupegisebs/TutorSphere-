using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.LessonCoverage;

public record CreateLessonCoverageRequest(
    Guid OriginalTenantId,
    Guid SubstituteTenantId,
    string Reason,
    Guid? UnavailabilityId = null,
    IReadOnlyList<Guid>? LessonIds = null,
    DateTime? WindowStart = null,
    DateTime? WindowEnd = null);

public record RespondLessonCoverageRequest(bool Approve);

public record LessonCoverageDto(
    Guid Id,
    Guid LessonId,
    Guid OriginalTenantId,
    Guid SubstituteTenantId,
    string OriginalTeacherName,
    string SubstituteTeacherName,
    string LessonTitle,
    string? Subject,
    DateTime StartTime,
    DateTime EndTime,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    decimal? TransferredTutorAmount,
    IReadOnlyList<string>? StudentNames = null);

public record LessonCoverageTeacherOptionDto(Guid TenantId, string Name);

public record UnavailableTeacherDto(
    Guid TenantId,
    string Name,
    Guid UnavailabilityId,
    DateTime StartTime,
    DateTime EndTime,
    string? Reason,
    int UpcomingLessonCount);
