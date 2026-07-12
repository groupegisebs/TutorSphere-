namespace TutorSphere.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    int? Age,
    bool IsMinor,
    bool IsAutonomous,
    Guid ParentProfileId,
    string? ParentName,
    string? PhotoUrl,
    string? SchoolLevel,
    string? SchoolName,
    IReadOnlyList<string> Subjects,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt);

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    Guid? ParentProfileId,
    string? SchoolLevel,
    string? SchoolName,
    string? Subjects,
    string? Notes);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    Guid? ParentProfileId,
    string? SchoolLevel,
    string? SchoolName,
    string? Subjects,
    string? Notes,
    bool? IsActive);
