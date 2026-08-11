using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Invitation à postuler envoyée par un expert à un enseignant.</summary>
public class TeacherApplicationInvite : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? PersonalMessage { get; set; }

    public string InvitedByUserId { get; set; } = string.Empty;
    public Guid ExpertGroupId { get; set; }
    public ExpertGroup? ExpertGroup { get; set; }

    /// <summary>Jeton inclus dans le lien d'inscription.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public TeacherApplicationInviteStatus Status { get; set; } = TeacherApplicationInviteStatus.Sent;

    public Guid? AcceptedTenantId { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
