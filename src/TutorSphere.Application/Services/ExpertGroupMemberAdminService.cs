using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertGroupMemberAdminService
{
    Task<GroupMemberDirectoryDto> GetDirectoryAsync(
        string callerUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task<GroupMemberActivityDto> GetActivityAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task UpdateRoleAsync(
        string callerUserId, string memberUserId, ExpertGroupMemberRole role, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task UpdatePermissionsAsync(
        string callerUserId, string memberUserId, IReadOnlyList<string> permissions, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task SuspendAsync(
        string callerUserId, string memberUserId, string reason, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task ReactivateAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task<GroupMemberRemovalCheckDto> PreviewRemoveAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
    Task RemoveAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null);
}

public class ExpertGroupMemberAdminService(
    IApplicationDbContext db,
    IExpertGroupManagerService managers,
    IExpertGovernanceAuditService audit,
    IUserContactLookup contacts) : IExpertGroupMemberAdminService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<GroupMemberDirectoryDto> GetDirectoryAsync(
        string callerUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        var members = db.ExpertGroupMembers
            .Where(m => m.ExpertGroupId == groupId && m.Status != ExpertMembershipStatus.Removed)
            .OrderByDescending(m => m.MemberRole == ExpertGroupMemberRole.Manager)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var pendingInviteStatuses = new[]
        {
            ExpertMembershipInviteStatus.Sent,
            ExpertMembershipInviteStatus.AcceptedByCandidate,
            ExpertMembershipInviteStatus.PendingMemberApproval,
            ExpertMembershipInviteStatus.AwaitingAdminValidation
        };
        var invites = db.ExpertMembershipInvites
            .Where(i => i.ExpertGroupId == groupId && pendingInviteStatuses.Contains(i.Status))
            .OrderByDescending(i => i.SentAtUtc)
            .ToList();

        var openTasks = db.ExpertDelegatedTasks
            .Where(t => t.ExpertGroupId == groupId
                        && (t.Status == ExpertDelegatedTaskStatus.Open || t.Status == ExpertDelegatedTaskStatus.InProgress))
            .GroupBy(t => t.AssigneeExpertUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.UserId, x => x.Count);

        var inviterIds = members.Select(m => m.InvitedByUserId)
            .Concat(invites.Select(i => i.InvitedByUserId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var inviterNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in inviterIds)
        {
            var c = await contacts.GetAsync(id!, ct);
            if (c is not null)
                inviterNames[id!] = c.Value.DisplayName;
        }

        var items = new List<GroupMemberDirectoryItemDto>();
        foreach (var m in members)
        {
            var perms = ReadPermissions(m);
            items.Add(new GroupMemberDirectoryItemDto(
                m.Id, "member", m.ExpertGroupId, m.UserId, "", "", null, m.Specialty,
                (int)m.MemberRole, (int)m.Status,
                m.AdmittedAtUtc ?? m.CreatedAt,
                m.InvitedByUserId,
                m.InvitedByUserId is { } ib && inviterNames.TryGetValue(ib, out var n) ? n : null,
                openTasks.GetValueOrDefault(m.UserId),
                perms,
                m.MemberRole == ExpertGroupMemberRole.Manager));
        }

        foreach (var i in invites)
        {
            var fullName = $"{i.FirstName} {i.LastName}".Trim();
            items.Add(new GroupMemberDirectoryItemDto(
                i.Id, "invite", i.ExpertGroupId, i.CandidateUserId, i.Email, fullName, i.Phone, i.Specialty,
                (int)ExpertMembershipGovernanceService.ParseIntendedRole(i.IntendedRole),
                10,
                i.SentAtUtc,
                i.InvitedByUserId,
                inviterNames.GetValueOrDefault(i.InvitedByUserId),
                0,
                [],
                false,
                i.Id));
        }

        var roster = members;
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var active = roster.Count(m => m.Status == ExpertMembershipStatus.Active);
        var seniors = roster.Count(m => m.Status == ExpertMembershipStatus.Active && IsSenior(m.MemberRole));
        var experts = roster.Count(m => m.Status == ExpertMembershipStatus.Active && m.MemberRole == ExpertGroupMemberRole.Expert);
        var suspended = roster.Count(m => m.Status == ExpertMembershipStatus.Suspended);
        var joinedMonth = roster.Count(m => (m.AdmittedAtUtc ?? m.CreatedAt) >= monthStart);

        return new GroupMemberDirectoryDto(items, active, joinedMonth, experts, seniors, suspended, invites.Count);
    }

    public Task<GroupMemberActivityDto> GetActivityAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        RequireMember(groupId, memberUserId);
        return Task.FromResult(BuildActivity(groupId, memberUserId));
    }

    public async Task UpdateRoleAsync(
        string callerUserId, string memberUserId, ExpertGroupMemberRole role, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        EnsureNotSelf(callerUserId, memberUserId);
        var member = RequireMember(groupId, memberUserId);
        if (member.MemberRole == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException("Le transfert de Responsable est un workflow distinct.");
        if (role == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException("Le rôle Responsable ne peut pas être attribué ici.");
        if (role is not (ExpertGroupMemberRole.Expert or ExpertGroupMemberRole.Senior
            or ExpertGroupMemberRole.Observer or ExpertGroupMemberRole.DisciplineLead
            or ExpertGroupMemberRole.CommitteeLead))
            throw new InvalidOperationException("Rôle inconnu.");

        var previous = member.MemberRole;
        member.MemberRole = role;
        member.PermissionsJson = WritePermissions(GroupMemberPermissionCatalog.Sanitize(role, ReadPermissions(member)));
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            ExpertGovernanceEventType.MemberRoleChanged,
            callerUserId,
            $"Rôle modifié : {previous} → {role}",
            groupId, relatedEntityId: member.Id, isNotification: false, ct: ct);
    }

    public async Task UpdatePermissionsAsync(
        string callerUserId, string memberUserId, IReadOnlyList<string> permissions, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        var member = RequireMember(groupId, memberUserId);
        if (member.MemberRole == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException("Le Responsable conserve l'ensemble des permissions du groupe.");
        member.PermissionsJson = WritePermissions(GroupMemberPermissionCatalog.Sanitize(member.MemberRole, permissions));
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            ExpertGovernanceEventType.MemberPermissionsUpdated,
            callerUserId,
            "Permissions mises à jour",
            groupId, relatedEntityId: member.Id, isNotification: false, ct: ct);
    }

    public async Task SuspendAsync(
        string callerUserId, string memberUserId, string reason, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        EnsureNotSelf(callerUserId, memberUserId);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Le motif de la suspension est obligatoire.");
        var member = RequireMember(groupId, memberUserId);
        if (member.MemberRole == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException("Impossible de suspendre le Responsable actif.");
        member.Status = ExpertMembershipStatus.Suspended;
        member.SuspendedAtUtc = DateTime.UtcNow;
        member.SuspensionReason = reason.Trim();
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            ExpertGovernanceEventType.MemberSuspended,
            callerUserId,
            $"Membre suspendu : {reason.Trim()}",
            groupId, relatedEntityId: member.Id, isNotification: true, ct: ct);
    }

    public async Task ReactivateAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        var member = RequireMember(groupId, memberUserId);
        if (member.Status != ExpertMembershipStatus.Suspended)
            throw new InvalidOperationException("Ce membre n'est pas suspendu.");
        member.Status = ExpertMembershipStatus.Active;
        member.EndedAtUtc = null;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            ExpertGovernanceEventType.MemberReactivated,
            callerUserId,
            "Membre réactivé",
            groupId, relatedEntityId: member.Id, isNotification: false, ct: ct);
    }

    public Task<GroupMemberRemovalCheckDto> PreviewRemoveAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        RequireMember(groupId, memberUserId);
        return Task.FromResult(BuildRemovalCheck(groupId, memberUserId));
    }

    public async Task RemoveAsync(
        string callerUserId, string memberUserId, CancellationToken ct = default, bool asPlatformAdmin = false, Guid? actAsGroupId = null)
    {
        var groupId = RequireManagedGroup(callerUserId, asPlatformAdmin, actAsGroupId);
        EnsureNotSelf(callerUserId, memberUserId);
        var member = RequireMember(groupId, memberUserId);
        if (member.MemberRole == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException("Impossible de retirer le Responsable actif. Transférez d'abord la responsabilité.");

        var check = BuildRemovalCheck(groupId, memberUserId);
        if (!check.CanRemove)
            throw new InvalidOperationException(RemovalMessage(check));

        member.Status = ExpertMembershipStatus.Removed;
        member.EndedAtUtc = DateTime.UtcNow;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            ExpertGovernanceEventType.MemberRemovedFromGroup,
            callerUserId,
            "Membre retiré du groupe",
            groupId, relatedEntityId: member.Id, isNotification: true, ct: ct);
    }

    private Guid RequireManagedGroup(string callerUserId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        if (asPlatformAdmin && actAsGroupId is Guid gid)
        {
            if (!db.ExpertGroups.Any(g => g.Id == gid && g.IsActive))
                throw new InvalidOperationException("Groupe introuvable ou inactif.");
            return gid;
        }

        if (!managers.IsActiveManager(callerUserId))
            throw new InvalidOperationException("Accès réservé au Responsable du groupe.");

        var membership = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.UserId == callerUserId
            && m.Status == ExpertMembershipStatus.Active
            && m.MemberRole == ExpertGroupMemberRole.Manager)
            ?? throw new InvalidOperationException("Responsable sans adhésion active.");
        return membership.ExpertGroupId;
    }

    private ExpertGroupMember RequireMember(Guid groupId, string userId) =>
        db.ExpertGroupMembers.FirstOrDefault(m =>
            m.ExpertGroupId == groupId
            && m.UserId == userId
            && m.Status != ExpertMembershipStatus.Removed)
        ?? throw new InvalidOperationException("Membre introuvable dans ce groupe.");

    private static void EnsureNotSelf(string callerUserId, string memberUserId)
    {
        if (string.Equals(callerUserId, memberUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Vous ne pouvez pas effectuer cette action sur votre propre compte.");
    }

    private GroupMemberActivityDto BuildActivity(Guid groupId, string userId)
    {
        var teachersApproved = db.Tenants.Count(t =>
            t.ApprovedByExpertGroupId == groupId
            && t.ApprovedByUserId == userId
            && t.ExpertApprovalStatus == ExpertApprovalStatus.Approved);
        var evaluations = db.Tenants.Count(t =>
            t.ApprovedByExpertGroupId == groupId
            && t.ApprovedByUserId == userId
            && (t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                || t.ExpertApprovalStatus == ExpertApprovalStatus.Rejected
                || t.ExpertApprovalStatus == ExpertApprovalStatus.ChangesRequested));
        var workspaceDone = db.ExpertWorkspaceItems.Count(i =>
            i.ExpertGroupId == groupId
            && i.CreatedByUserId == userId
            && i.Status == ExpertWorkspaceItemStatus.Done);
        var openTasks = db.ExpertDelegatedTasks.Count(t =>
            t.ExpertGroupId == groupId
            && t.AssigneeExpertUserId == userId
            && (t.Status == ExpertDelegatedTaskStatus.Open || t.Status == ExpertDelegatedTaskStatus.InProgress));
        var pendingVotes = CountPendingVotes(groupId, userId);
        return new GroupMemberActivityDto(evaluations + workspaceDone, teachersApproved, openTasks, pendingVotes);
    }

    private GroupMemberRemovalCheckDto BuildRemovalCheck(Guid groupId, string userId)
    {
        var openTasks = db.ExpertDelegatedTasks.Count(t =>
            t.ExpertGroupId == groupId
            && t.AssigneeExpertUserId == userId
            && (t.Status == ExpertDelegatedTaskStatus.Open || t.Status == ExpertDelegatedTaskStatus.InProgress));
        var openEvals = db.Tenants.Count(t =>
            t.ReviewAssignedToUserId == userId
            && t.ApprovedByExpertGroupId == groupId
            && (t.ExpertApprovalStatus == ExpertApprovalStatus.Assigned
                || t.ExpertApprovalStatus == ExpertApprovalStatus.UnderReview));
        var demos = db.ExpertWorkspaceItems.Count(i =>
            i.ExpertGroupId == groupId
            && i.ItemType == ExpertWorkspaceItemType.Demonstration
            && i.Status != ExpertWorkspaceItemStatus.Done
            && i.Status != ExpertWorkspaceItemStatus.Cancelled
            && (i.AssignedToUserId == userId || i.CreatedByUserId == userId));
        var votes = CountPendingVotes(groupId, userId);
        var can = openTasks + openEvals + demos + votes == 0;
        return new GroupMemberRemovalCheckDto(openTasks, openEvals, demos, votes, can);
    }

    private int CountPendingVotes(Guid groupId, string userId) =>
        db.ExpertMembershipInvites.Count(i =>
            i.ExpertGroupId == groupId
            && i.Status == ExpertMembershipInviteStatus.PendingMemberApproval
            && i.EligibleVoterUserIdsCsv != null
            && i.EligibleVoterUserIdsCsv.Contains(userId)
            && !db.ExpertMembershipVotes.Any(v => v.InviteId == i.Id && v.VoterUserId == userId));

    private static string RemovalMessage(GroupMemberRemovalCheckDto check)
    {
        var parts = new List<string>();
        if (check.OpenTasks > 0) parts.Add($"{check.OpenTasks} tâche{(check.OpenTasks > 1 ? "s" : "")} ouverte{(check.OpenTasks > 1 ? "s" : "")}");
        if (check.OpenEvaluations > 0) parts.Add($"{check.OpenEvaluations} évaluation{(check.OpenEvaluations > 1 ? "s" : "")} en cours");
        if (check.AssignedDemonstrations > 0) parts.Add($"{check.AssignedDemonstrations} démonstration{(check.AssignedDemonstrations > 1 ? "s" : "")} assignée{(check.AssignedDemonstrations > 1 ? "s" : "")}");
        if (check.PendingVotes > 0) parts.Add($"{check.PendingVotes} vote{(check.PendingVotes > 1 ? "s" : "")} actif{(check.PendingVotes > 1 ? "s" : "")}");
        return "Ce membre possède encore : " + string.Join(", ", parts) + ". Réattribuez ces éléments avant de le retirer.";
    }

    private static bool IsSenior(ExpertGroupMemberRole role) =>
        role is ExpertGroupMemberRole.Senior or ExpertGroupMemberRole.DisciplineLead or ExpertGroupMemberRole.CommitteeLead;

    private static IReadOnlyList<string> ReadPermissions(ExpertGroupMember member)
    {
        List<string>? stored = null;
        if (!string.IsNullOrWhiteSpace(member.PermissionsJson))
        {
            try { stored = JsonSerializer.Deserialize<List<string>>(member.PermissionsJson, JsonOpts); }
            catch { stored = null; }
        }
        if (stored is { Count: > 0 })
            return GroupMemberPermissionCatalog.Sanitize(member.MemberRole, stored);
        return GroupMemberPermissionCatalog.DefaultsFor(member.MemberRole);
    }

    private static string WritePermissions(IReadOnlyList<string> keys) =>
        JsonSerializer.Serialize(keys);
}
