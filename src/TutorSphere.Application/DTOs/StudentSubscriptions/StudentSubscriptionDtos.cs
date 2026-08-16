namespace TutorSphere.Application.DTOs.StudentSubscriptions;

public record EnrollStudentRequest(Guid StudentId, Guid OfferingId);

public record EnrollSelfRequest(Guid OfferingId);

public record StudentSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid StudentId,
    string StudentName,
    Guid OfferingId,
    string OfferingTitle,
    string? Subject,
    decimal Price,
    string Currency,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    int SessionsRemaining,
    string? ParentName = null,
    string? TeacherName = null,
    DateTime? RequestedAt = null);

public record ExpertPendingEnrollmentDto(
    Guid Id,
    Guid TenantId,
    string TeacherName,
    Guid StudentId,
    string StudentName,
    string? ParentName,
    Guid OfferingId,
    string OfferingTitle,
    decimal Price,
    string Currency,
    DateTime RequestedAt,
    string Status);
