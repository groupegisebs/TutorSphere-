namespace TutorSphere.Application.DTOs.Parents;

public record ParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int ChildrenCount);

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
