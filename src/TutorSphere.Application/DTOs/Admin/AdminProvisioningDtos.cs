using TutorSphere.Application.DTOs.SubscriptionOfferings;

namespace TutorSphere.Application.DTOs.Admin;

public record AdminCreateParentRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone = null);

public record AdminCreateStudentRequest(
    string Email,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string? Phone = null,
    /// <summary>E-mail du parent existant (obligatoire si âge &lt; 14).</summary>
    string? ParentEmail = null);

public record AdminCreateTeacherRequest(
    string Email,
    string FirstName,
    string LastName,
    Guid ExpertGroupId,
    string? SchoolName = null,
    string? Slug = null,
    string? City = null,
    string? Phone = null,
    bool ActivateSchool = true,
    /// <summary>Offre de service initiale (optionnelle) créée pour l'école.</summary>
    CreateSubscriptionOfferingRequest? InitialOffering = null);

public record AdminCreatedAccountDto(
    string UserId,
    string Email,
    string FullName,
    string Role,
    string TemporaryPassword,
    bool CredentialsSent,
    Guid? TenantId = null,
    string? TenantSlug = null,
    Guid? ExpertGroupId = null,
    string? ExpertGroupName = null,
    Guid? OfferingId = null);
