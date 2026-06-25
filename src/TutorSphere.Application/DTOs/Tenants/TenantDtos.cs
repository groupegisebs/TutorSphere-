namespace TutorSphere.Application.DTOs.Tenants;

public record CreateTenantRequest(
    string Name,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerFirstName,
    string OwnerLastName,
    string? City,
    string? Country);

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? Subdomain,
    string Status,
    string Plan,
    string Currency,
    string Language);

public record TenantDashboardDto(
    decimal MonthlyRevenue,
    int ActiveStudents,
    int ActiveSubscriptions,
    int UpcomingLessons,
    int PendingPayments);
