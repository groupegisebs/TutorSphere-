namespace TutorSphere.Application.DTOs.Parents;

public record ParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int ChildrenCount,
    int UnreadMessagesCount = 0,
    string? Country = null);

public record CreateParentRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Country = null);

public record UpdateParentRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Country = null);

public record ParentAddChildRequest(
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    string? Email,
    string? SchoolLevel,
    string? SchoolName,
    string? Subjects);

public record ParentUpdateChildRequest(
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    string? Email,
    string? SchoolLevel,
    string? SchoolName,
    string? Subjects);
