namespace TutorSphere.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    DateTime? DateOfBirth,
    int? Age,
    bool IsMinor,
    bool IsAutonomous,
    Guid ParentProfileId,
    string? ParentName,
    string? PhotoUrl,
    string? SchoolLevel,
    string? SchoolName,
    IReadOnlyList<string> Subjects);

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string? Email,
    DateTime? DateOfBirth,
    Guid? ParentProfileId);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    string? Email,
    DateTime? DateOfBirth,
    Guid? ParentProfileId);
