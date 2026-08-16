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

/// <summary>
/// Vue enseignant d'un parent : identité et enfants inscrits à ses cours, sans coordonnées.
/// Le contact se fait uniquement par la messagerie interne.
/// </summary>
public record TutorParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    int ChildrenCount,
    IReadOnlyList<string> Children,
    string? MessagingUserId);

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
