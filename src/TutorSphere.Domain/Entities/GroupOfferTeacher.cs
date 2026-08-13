using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class GroupOfferTeacher : BaseEntity
{
    public Guid GroupOfferId { get; set; }
    public Guid TeacherTenantId { get; set; }

    public GroupOfferTeacherAssignmentStatus AssignmentStatus { get; set; } =
        GroupOfferTeacherAssignmentStatus.Invited;

    public decimal? TeacherPrice { get; set; }
    public int? Capacity { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string? ApprovedByUserId { get; set; }

    public GroupOffer GroupOffer { get; set; } = null!;
    public Tenant TeacherTenant { get; set; } = null!;
}
