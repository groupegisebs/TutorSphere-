using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

public enum ParentSupportRequestStatus
{
    Open = 0,
    InProgress = 1,
    Closed = 2
}

/// <summary>Demande d'aide / contact support depuis l'espace parent.</summary>
public class ParentSupportRequest : BaseEntity
{
    public Guid ParentProfileId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public ParentSupportRequestStatus Status { get; set; } = ParentSupportRequestStatus.Open;
    public string? CaseNumber { get; set; }
    public Guid? StudentId { get; set; }
    public string? Reason { get; set; }

    public ParentProfile Parent { get; set; } = null!;
}
