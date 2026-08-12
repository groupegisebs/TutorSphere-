using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

/// <summary>Remarque persistante laissée par un expert à un enseignant suivi (activité, matériel, général).</summary>
public class ExpertRemark : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Groupe d'experts auteur (SetNull si le groupe est supprimé).</summary>
    public Guid? ExpertGroupId { get; set; }

    public string AuthorUserId { get; set; } = string.Empty;
    public ExpertRemarkCategory Category { get; set; } = ExpertRemarkCategory.General;
    public string Message { get; set; } = string.Empty;

    /// <summary>Remarque liée à un devoir précis, le cas échéant.</summary>
    public Guid? RelatedHomeworkId { get; set; }

    /// <summary>Remarque liée à un document précis, le cas échéant.</summary>
    public Guid? RelatedDocumentId { get; set; }

    public DateTime? ReadByTeacherAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
