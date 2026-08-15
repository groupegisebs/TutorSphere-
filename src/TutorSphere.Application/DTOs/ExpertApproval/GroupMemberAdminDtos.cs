using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public static class GroupMemberPermissionCatalog
{
    public const string TeachersView = "teachers.view";
    public const string TeachersAdd = "teachers.add";
    public const string TeachersEvaluate = "teachers.evaluate";
    public const string TeachersApprove = "teachers.approve";
    public const string TeachersSuspend = "teachers.suspend";
    public const string AdmissionsView = "admissions.view";
    public const string AdmissionsVote = "admissions.vote";
    public const string AdmissionsReview = "admissions.review";
    public const string DemosPlan = "demos.plan";
    public const string DemosEvaluate = "demos.evaluate";
    public const string DemosView = "demos.view";
    public const string GroupMembersView = "group.members.view";
    public const string GroupTasksAssign = "group.tasks.assign";
    public const string GroupDocuments = "group.documents.manage";

    public static readonly IReadOnlyList<(string Category, string Key, string Label)> All =
    [
        ("Enseignants", TeachersView, "Voir les enseignants"),
        ("Enseignants", TeachersAdd, "Ajouter un enseignant"),
        ("Enseignants", TeachersEvaluate, "Évaluer un enseignant"),
        ("Enseignants", TeachersApprove, "Approuver un enseignant"),
        ("Enseignants", TeachersSuspend, "Suspendre un enseignant"),
        ("Admissions", AdmissionsView, "Voir les admissions"),
        ("Admissions", AdmissionsVote, "Participer aux votes"),
        ("Admissions", AdmissionsReview, "Examiner une candidature"),
        ("Démonstrations", DemosPlan, "Planifier"),
        ("Démonstrations", DemosEvaluate, "Évaluer"),
        ("Démonstrations", DemosView, "Consulter"),
        ("Groupe", GroupMembersView, "Voir les membres"),
        ("Groupe", GroupTasksAssign, "Attribuer des tâches"),
        ("Groupe", GroupDocuments, "Gérer les documents")
    ];

    private static readonly HashSet<string> Elevated = new(StringComparer.Ordinal)
    {
        TeachersApprove, TeachersSuspend, AdmissionsReview, GroupTasksAssign, GroupDocuments
    };

    public static IReadOnlyList<string> DefaultsFor(ExpertGroupMemberRole role) => role switch
    {
        ExpertGroupMemberRole.Manager => All.Select(a => a.Key).ToArray(),
        ExpertGroupMemberRole.Senior or ExpertGroupMemberRole.DisciplineLead or ExpertGroupMemberRole.CommitteeLead =>
        [
            TeachersView, TeachersAdd, TeachersEvaluate, TeachersApprove,
            AdmissionsView, AdmissionsVote, AdmissionsReview,
            DemosPlan, DemosEvaluate, DemosView,
            GroupMembersView, GroupTasksAssign, GroupDocuments
        ],
        ExpertGroupMemberRole.Observer =>
        [
            TeachersView, AdmissionsView, DemosView, GroupMembersView
        ],
        _ =>
        [
            TeachersView, TeachersAdd, TeachersEvaluate, TeachersApprove,
            AdmissionsView, AdmissionsVote,
            DemosPlan, DemosEvaluate, DemosView,
            GroupMembersView
        ]
    };

    public static IReadOnlyList<string> Sanitize(ExpertGroupMemberRole role, IEnumerable<string>? keys)
    {
        var allowed = All.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var picked = (keys ?? []).Where(k => allowed.Contains(k)).Distinct(StringComparer.Ordinal).ToList();
        if (role == ExpertGroupMemberRole.Manager)
            return All.Select(a => a.Key).ToList();
        if (role == ExpertGroupMemberRole.Observer)
            picked = picked.Where(k => !Elevated.Contains(k)).ToList();
        return picked;
    }

    public static IReadOnlyList<string> Responsibilities(IEnumerable<string> keys)
    {
        var set = keys.ToHashSet(StringComparer.Ordinal);
        var labels = new List<string>();
        if (set.Contains(TeachersEvaluate) || set.Contains(TeachersApprove))
            labels.Add("Évaluation enseignants");
        if (set.Contains(AdmissionsVote) || set.Contains(AdmissionsReview))
            labels.Add("Admissions experts");
        if (set.Contains(DemosPlan) || set.Contains(DemosEvaluate))
            labels.Add("Démonstrations pédagogiques");
        if (set.Contains(GroupTasksAssign))
            labels.Add("Tâches & délégations");
        if (set.Contains(GroupDocuments))
            labels.Add("Documents du groupe");
        return labels;
    }
}

public record GroupMemberDirectoryItemDto(
    Guid Id,
    string Kind,
    Guid ExpertGroupId,
    string? UserId,
    string Email,
    string FullName,
    string? Phone,
    string? Specialty,
    int Role,
    int Status,
    DateTime? JoinedAtUtc,
    string? InvitedByUserId,
    string? InvitedByName,
    int OpenTaskCount,
    IReadOnlyList<string> Permissions,
    bool IsManager,
    Guid? InviteId = null);

public record GroupMemberActivityDto(
    int EvaluationsCompleted,
    int TeachersApproved,
    int OpenTasks,
    int PendingVotes);

public record GroupMemberDirectoryDto(
    IReadOnlyList<GroupMemberDirectoryItemDto> Items,
    int ActiveCount,
    int JoinedThisMonth,
    int ExpertCount,
    int SeniorCount,
    int SuspendedCount,
    int PendingInviteCount);

public record UpdateGroupMemberRoleRequest(int Role);
public record UpdateGroupMemberPermissionsRequest(IReadOnlyList<string> Permissions);
public record SuspendGroupMemberRequest(string Reason);

public record GroupMemberRemovalCheckDto(
    int OpenTasks,
    int OpenEvaluations,
    int AssignedDemonstrations,
    int PendingVotes,
    bool CanRemove);
