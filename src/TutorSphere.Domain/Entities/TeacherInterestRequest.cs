using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Demande d'intérêt publique — n'est pas encore un compte enseignant.</summary>
public class TeacherInterestRequest : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public string? Disciplines { get; set; }
    public string? Experience { get; set; }
    public string? Message { get; set; }

    public Guid? RoutedExpertGroupId { get; set; }
    public TeacherInterestRequestStatus Status { get; set; } = TeacherInterestRequestStatus.Submitted;
    public string? HandledByUserId { get; set; }
    public Guid? TeacherInviteId { get; set; }

    public ExpertGroup? RoutedExpertGroup { get; set; }
}