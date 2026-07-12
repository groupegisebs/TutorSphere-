namespace TutorSphere.Application.DTOs.StudentSubscriptions;

public record EnrollStudentRequest(Guid StudentId, Guid OfferingId);

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
    string? ParentName = null);
