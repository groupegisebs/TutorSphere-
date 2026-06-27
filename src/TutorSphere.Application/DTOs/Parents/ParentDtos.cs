namespace TutorSphere.Application.DTOs.Parents;

public record ParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int ChildrenCount,
    int UnreadMessagesCount = 0);

public record CreateParentRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone);

public record UpdateParentRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone);

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
