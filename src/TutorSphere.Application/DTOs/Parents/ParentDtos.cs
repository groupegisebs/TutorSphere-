using TutorSphere.Application.Common;

namespace TutorSphere.Application.DTOs.Parents;

/// <param name="PendingPaymentsTotals">
/// Un total par devise plutôt qu'un montant unique : une famille peut avoir une offre en CAD et
/// une autre en XAF, et la somme des deux ne signifierait rien.
/// </param>
public record ParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int ChildrenCount,
    int UnreadMessagesCount = 0,
    string? Country = null,
    int PendingPaymentsCount = 0,
    IReadOnlyList<MoneyTotal>? PendingPaymentsTotals = null);

/// <summary>
/// Enfant vu par l'enseignant : nom et pays de résidence uniquement (pas d'adresse parent).
/// </summary>
public record TutorChildSummaryDto(
    string FirstName,
    string LastName,
    string? Country);

/// <summary>
/// Vue enseignant d'un parent : identité et enfants inscrits à ses cours, sans coordonnées.
/// Le contact se fait uniquement par la messagerie interne.
/// </summary>
public record TutorParentDto(
    Guid Id,
    string FirstName,
    string LastName,
    int ChildrenCount,
    IReadOnlyList<TutorChildSummaryDto> Children,
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
