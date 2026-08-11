using TutorSphere.Domain.Common;

namespace TutorSphere.Domain.Entities;

/// <summary>Lien utilisateur Identity ↔ groupe d'experts.</summary>
public class ExpertGroupMember : BaseEntity
{
    public Guid ExpertGroupId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public ExpertGroup ExpertGroup { get; set; } = null!;
}
