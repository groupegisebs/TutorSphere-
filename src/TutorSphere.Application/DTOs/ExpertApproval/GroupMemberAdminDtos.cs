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
    public const string MeetingsView = "meetings.view";
    public const string MeetingsCreate = "meetings.create";
    public const string MeetingsUpdateOwn = "meetings.updateOwn";
    public const string MeetingsUpdateGroup = "meetings.updateGroup";
    public const string MeetingsCancel = "meetings.cancel";
    public const string MeetingsInviteInternal = "meetings.inviteInternal";
    public const string MeetingsInviteExternal = "meetings.inviteExternal";
    public const string MeetingsModerate = "meetings.moderate";
    public const string MeetingsRecord = "meetings.record";
    public const string MeetingsEnableAi = "meetings.enableAi";
    public const string MeetingsViewTranscript = "meetings.viewTranscript";
    public const string MeetingsManageMinutes = "meetings.manageMinutes";

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
        ("Groupe", GroupDocuments, "Gérer les documents"),
        ("Réunions", MeetingsView, "Voir les réunions"),
        ("Réunions", MeetingsCreate, "Créer une réunion"),
        ("Réunions", MeetingsUpdateOwn, "Modifier ses réunions"),
        ("Réunions", MeetingsUpdateGroup, "Modifier les réunions du groupe"),
        ("Réunions", MeetingsCancel, "Annuler une réunion"),
        ("Réunions", MeetingsInviteInternal, "Inviter des membres"),
        ("Réunions", MeetingsInviteExternal, "Inviter des personnes externes"),
        ("Réunions", MeetingsModerate, "Modérer la salle"),
        ("Réunions", MeetingsRecord, "Enregistrer"),
        ("Réunions", MeetingsEnableAi, "Activer l’assistant IA"),
        ("Réunions", MeetingsViewTranscript, "Voir la transcription"),
        ("Réunions", MeetingsManageMinutes, "Gérer le compte rendu")
    ];

    private static readonly HashSet<string> Elevated = new(StringComparer.Ordinal)
    {
        TeachersApprove, TeachersSuspend, AdmissionsReview, GroupTasksAssign, GroupDocuments,
        MeetingsModerate, MeetingsRecord, MeetingsEnableAi, MeetingsManageMinutes
    };

    public static bool IsElevated(string key) => Elevated.Contains(key);

    public static IReadOnlyList<string> LostElevated(IEnumerable<string> previous, IEnumerable<string> next)
    {
        var nextSet = next.ToHashSet(StringComparer.Ordinal);
        return previous.Where(k => Elevated.Contains(k) && !nextSet.Contains(k)).Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> DefaultsFor(ExpertGroupMemberRole role) => role switch
    {
        ExpertGroupMemberRole.Manager => All.Select(a => a.Key).ToArray(),
        ExpertGroupMemberRole.Senior or ExpertGroupMemberRole.DisciplineLead or ExpertGroupMemberRole.CommitteeLead =>
        [
            TeachersView, TeachersAdd, TeachersEvaluate, TeachersApprove,
            AdmissionsView, AdmissionsVote, AdmissionsReview,
            DemosPlan, DemosEvaluate, DemosView,
            GroupMembersView, GroupTasksAssign, GroupDocuments,
            MeetingsView, MeetingsCreate, MeetingsUpdateOwn, MeetingsUpdateGroup, MeetingsCancel,
            MeetingsInviteInternal, MeetingsInviteExternal, MeetingsModerate, MeetingsRecord,
            MeetingsEnableAi, MeetingsViewTranscript, MeetingsManageMinutes
        ],
        ExpertGroupMemberRole.Observer =>
        [
            TeachersView, AdmissionsView, DemosView, GroupMembersView, MeetingsView
        ],
        _ =>
        [
            TeachersView, TeachersAdd, TeachersEvaluate, TeachersApprove,
            AdmissionsView, AdmissionsVote,
            DemosPlan, DemosEvaluate, DemosView,
            GroupMembersView,
            MeetingsView, MeetingsCreate, MeetingsUpdateOwn, MeetingsInviteInternal, MeetingsViewTranscript
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

/// <summary>Catalogue des rôles métier d'un groupe (hors mandat Responsable du groupe).</summary>
public static class GroupDefinedRoleCatalog
{
    public const string Expert = "expert";
    public const string Pedagogy = "pedagogy";
    public const string Teachers = "teachers";
    public const string Offers = "offers";
    public const string Admissions = "admissions";
    public const string Quality = "quality";
    public const string Moderator = "moderator";
    public const string Member = "member";

    public sealed record BuiltIn(
        string Key,
        string Name,
        string Description,
        string BadgeColor,
        IReadOnlyList<string> Permissions,
        bool SuperAdminOnly,
        ExpertGroupMemberRole MemberRole);

    public static readonly IReadOnlyList<BuiltIn> All =
    [
        new(Expert, "Expert",
            "Membre expert : évaluations, démonstrations et admissions courantes.",
            "#2563EB", GroupMemberPermissionCatalog.DefaultsFor(ExpertGroupMemberRole.Expert), false,
            ExpertGroupMemberRole.Expert),
        new(Pedagogy, "Responsable pédagogique",
            "Pilote les démonstrations et la qualité pédagogique du groupe.",
            "#0f766e",
            [
                GroupMemberPermissionCatalog.TeachersView, GroupMemberPermissionCatalog.TeachersEvaluate,
                GroupMemberPermissionCatalog.DemosPlan, GroupMemberPermissionCatalog.DemosEvaluate,
                GroupMemberPermissionCatalog.DemosView, GroupMemberPermissionCatalog.GroupMembersView
            ], false, ExpertGroupMemberRole.Senior),
        new(Teachers, "Responsable des enseignants",
            "Gère le cycle de vie des enseignants du groupe.",
            "#1d4ed8",
            [
                GroupMemberPermissionCatalog.TeachersView, GroupMemberPermissionCatalog.TeachersAdd,
                GroupMemberPermissionCatalog.TeachersEvaluate, GroupMemberPermissionCatalog.TeachersApprove,
                GroupMemberPermissionCatalog.TeachersSuspend, GroupMemberPermissionCatalog.GroupMembersView
            ], false, ExpertGroupMemberRole.DisciplineLead),
        new(Offers, "Responsable des offres",
            "Suit le catalogue d'offres et les documents associés.",
            "#7c3aed",
            [
                GroupMemberPermissionCatalog.TeachersView, GroupMemberPermissionCatalog.GroupDocuments,
                GroupMemberPermissionCatalog.GroupTasksAssign, GroupMemberPermissionCatalog.GroupMembersView
            ], false, ExpertGroupMemberRole.Senior),
        new(Admissions, "Responsable des admissions",
            "Pilote les candidatures et les votes d'admission.",
            "#b45309",
            [
                GroupMemberPermissionCatalog.AdmissionsView, GroupMemberPermissionCatalog.AdmissionsVote,
                GroupMemberPermissionCatalog.AdmissionsReview, GroupMemberPermissionCatalog.GroupMembersView
            ], false, ExpertGroupMemberRole.CommitteeLead),
        new(Quality, "Responsable qualité",
            "Contrôle évaluations, démonstrations et admissions sensibles.",
            "#be123c",
            [
                GroupMemberPermissionCatalog.TeachersView, GroupMemberPermissionCatalog.TeachersEvaluate,
                GroupMemberPermissionCatalog.AdmissionsReview, GroupMemberPermissionCatalog.DemosEvaluate,
                GroupMemberPermissionCatalog.DemosView, GroupMemberPermissionCatalog.GroupMembersView
            ], false, ExpertGroupMemberRole.Senior),
        new(Moderator, "Modérateur/responsable du groupe",
            "Rôle opérationnel étendu. Créé uniquement par un Super Admin TutorSphere.",
            "#006D44", GroupMemberPermissionCatalog.DefaultsFor(ExpertGroupMemberRole.Manager), true,
            ExpertGroupMemberRole.CommitteeLead),
        new(Member, "Membre",
            "Accès de consultation au groupe, sans droits d'approbation.",
            "#64748b", GroupMemberPermissionCatalog.DefaultsFor(ExpertGroupMemberRole.Observer), false,
            ExpertGroupMemberRole.Observer)
    ];

    public static BuiltIn? Find(string? key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeName(string? name) => (name ?? "").Trim().ToUpperInvariant();

    public static bool IsManagerTitle(string? name)
    {
        var n = NormalizeName(name);
        return n is "RESPONSABLE DU GROUPE" or "RESPONSABLE" or "GROUP MANAGER" or "GESTIONNAIRE DU GROUPE";
    }

    public static BuiltIn? FindByName(string? name)
    {
        var n = NormalizeName(name);
        return All.FirstOrDefault(x => NormalizeName(x.Name) == n);
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
    Guid? InviteId = null,
    Guid? DefinedRoleId = null,
    string? DefinedRoleName = null,
    string? DefinedRoleColor = null,
    string? DefinedRoleKey = null);

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

public record UpdateGroupMemberRoleRequest(
    int? Role = null,
    Guid? DefinedRoleId = null,
    string? SystemKey = null);

public record CreateGroupDefinedRoleRequest(
    string Name,
    string? Description = null,
    string? BadgeColor = null,
    IReadOnlyList<string>? Permissions = null);

public record GroupDefinedRoleDto(
    Guid? Id,
    string Key,
    string Name,
    string? Description,
    string BadgeColor,
    IReadOnlyList<string> Permissions,
    bool IsCustom,
    bool SuperAdminOnly);
public record UpdateGroupMemberPermissionsRequest(IReadOnlyList<string> Permissions);
public record SuspendGroupMemberRequest(string Reason);

public record GroupMemberRemovalCheckDto(
    int OpenTasks,
    int OpenEvaluations,
    int AssignedDemonstrations,
    int PendingVotes,
    bool CanRemove);
