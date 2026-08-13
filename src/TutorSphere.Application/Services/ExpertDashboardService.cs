using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertDashboardService
{
    Task<ExpertDashboardSummaryDto> GetSummaryAsync(string expertUserId, CancellationToken ct = default);
}

public class ExpertDashboardService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups) : IExpertDashboardService
{
    private static readonly ExpertApprovalStatus[] PipelineStatuses =
    [
        ExpertApprovalStatus.Pending,
        ExpertApprovalStatus.Assigned,
        ExpertApprovalStatus.UnderReview,
        ExpertApprovalStatus.ChangesRequested
    ];

    public Task<ExpertDashboardSummaryDto> GetSummaryAsync(string expertUserId, CancellationToken ct = default)
    {
        var membership = db.ExpertGroupMembers
            .FirstOrDefault(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            ?? throw new InvalidOperationException("Vous n'êtes pas membre actif d'un groupe d'experts.");

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == membership.ExpertGroupId && g.IsActive)
            ?? throw new InvalidOperationException("Votre groupe d'experts est inactif.");

        var groupId = group.Id;
        var now = DateTime.UtcNow;
        var recentCutoff = now.AddDays(-30);
        var inactiveCutoff = now.AddDays(-45);

        var routedPending = db.Tenants
            .Where(t => PipelineStatuses.Contains(t.ExpertApprovalStatus))
            .ToList()
            .Where(t =>
            {
                var suggested = expertGroups.ResolveReviewerGroup(t.Country);
                return suggested is not null && suggested.Id == groupId;
            })
            .ToList();

        var approved = db.Tenants
            .Where(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Approved
                        && t.ApprovedByExpertGroupId == groupId)
            .Select(t => t.Id)
            .ToList();

        var suspended = db.Tenants.Count(t =>
            t.ExpertApprovalStatus == ExpertApprovalStatus.Suspended
            && t.ApprovedByExpertGroupId == groupId);

        var teacherPending = routedPending.Count(t => t.ExpertApprovalStatus == ExpertApprovalStatus.Pending);
        var underReview = routedPending.Count(t =>
            t.ExpertApprovalStatus is ExpertApprovalStatus.UnderReview or ExpertApprovalStatus.Assigned);
        var changes = routedPending.Count(t => t.ExpertApprovalStatus == ExpertApprovalStatus.ChangesRequested);

        var expertVotesOpen = db.ExpertMembershipInvites.Count(i =>
            i.ExpertGroupId == groupId
            && (i.Status == ExpertMembershipInviteStatus.PendingMemberApproval
                || i.Status == ExpertMembershipInviteStatus.AwaitingAdminValidation));

        var expertInvitesPending = db.ExpertMembershipInvites.Count(i =>
            i.ExpertGroupId == groupId && i.Status == ExpertMembershipInviteStatus.Sent);

        var teacherInvitesSent = db.TeacherApplicationInvites.Count(i =>
            i.ExpertGroupId == groupId && i.Status == TeacherApplicationInviteStatus.Sent);

        var unreadRemarks = approved.Count == 0
            ? 0
            : db.ExpertRemarksForAnyTenant.Count(r =>
                approved.Contains(r.TenantId) && r.ReadByTeacherAt == null);

        var cancelledRecent = approved.Count == 0
            ? 0
            : db.LessonsForAnyTenant.Count(l =>
                approved.Contains(l.TenantId)
                && l.StartTime >= recentCutoff
                && l.SettlementStatus == LessonSettlementStatus.CancelledFree);

        var disciplines = db.Disciplines.Where(d => d.ExpertGroupId == groupId && d.IsActive).Select(d => d.Id).ToList();
        var assignedDisciplineIds = db.TeacherDisciplineAssignments
            .Where(a => disciplines.Contains(a.DisciplineId))
            .Select(a => a.DisciplineId)
            .Distinct()
            .ToHashSet();
        var disciplinesWithout = disciplines.Count(id => !assignedDisciplineIds.Contains(id));

        var lastActivityByTenant = approved.Count == 0
            ? new Dictionary<Guid, DateTime?>()
            : db.LessonsForAnyTenant
                .Where(l => approved.Contains(l.TenantId))
                .GroupBy(l => l.TenantId)
                .Select(g => new { g.Key, Last = (DateTime?)g.Max(x => x.StartTime) })
                .ToList()
                .ToDictionary(x => x.Key, x => x.Last);

        var inactive = approved.Count(id =>
            !lastActivityByTenant.TryGetValue(id, out var last) || last is null || last < inactiveCutoff);

        var activeMembers = db.ExpertGroupMembers.Count(m =>
            m.ExpertGroupId == groupId && m.Status == ExpertMembershipStatus.Active);

        var nextPending = routedPending
            .OrderByDescending(t => t.ReviewPriority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefault();

        var nextVote = db.ExpertMembershipInvites
            .Where(i => i.ExpertGroupId == groupId
                        && i.Status == ExpertMembershipInviteStatus.PendingMemberApproval)
            .OrderBy(i => i.VoteExpiresAtUtc)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefault();

        var alerts = new List<ExpertDashboardAlertDto>();

        var stale = routedPending.Where(t => (now - t.CreatedAt).TotalDays >= 7).ToList();
        if (stale.Count > 0)
        {
            alerts.Add(new ExpertDashboardAlertDto(
                "warning",
                "stale_queue",
                $"{stale.Count} dossier(s) en attente depuis 7 jours ou plus.",
                "/expert/approvals"));
        }

        if (expertVotesOpen > 0)
        {
            var soon = db.ExpertMembershipInvites.Count(i =>
                i.ExpertGroupId == groupId
                && i.Status == ExpertMembershipInviteStatus.PendingMemberApproval
                && i.VoteExpiresAtUtc != null
                && i.VoteExpiresAtUtc < now.AddDays(3));
            if (soon > 0)
            {
                alerts.Add(new ExpertDashboardAlertDto(
                    "danger",
                    "vote_expiring",
                    $"{soon} candidature(s) Expert expire(nt) dans moins de 3 jours.",
                    "/expert/admissions"));
            }
        }

        if (disciplinesWithout > 0)
        {
            alerts.Add(new ExpertDashboardAlertDto(
                "info",
                "discipline_empty",
                $"{disciplinesWithout} discipline(s) sans enseignant affecté.",
                "/expert/disciplines"));
        }

        if (unreadRemarks > 0)
        {
            alerts.Add(new ExpertDashboardAlertDto(
                "info",
                "unread_remarks",
                $"{unreadRemarks} remarque(s) non lue(s) par les enseignants.",
                "/expert/teachers"));
        }

        if (activeMembers < 2)
        {
            alerts.Add(new ExpertDashboardAlertDto(
                "warning",
                "low_members",
                "Le groupe a moins de 2 membres actifs — les admissions Expert nécessitent l'admin ou un vote limité.",
                "/expert/admissions"));
        }

        if (inactive > 0)
        {
            alerts.Add(new ExpertDashboardAlertDto(
                "warning",
                "inactive_teachers",
                $"{inactive} enseignant(s) sans activité récente (45 jours).",
                "/expert/teachers"));
        }

        return Task.FromResult(new ExpertDashboardSummaryDto(
            group.Name,
            group.CountryCode,
            teacherPending,
            underReview,
            changes,
            approved.Count,
            suspended,
            expertVotesOpen,
            expertInvitesPending,
            teacherInvitesSent,
            unreadRemarks,
            cancelledRecent,
            disciplinesWithout,
            inactive,
            activeMembers,
            nextPending?.Id,
            nextVote,
            alerts));
    }
}
