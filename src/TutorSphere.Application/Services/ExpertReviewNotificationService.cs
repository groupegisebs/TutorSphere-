using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertReviewNotificationService
{
    /// <summary>
    /// Envoie une fois l'e-mail « enseignant en attente » aux membres du groupe d'experts responsable.
    /// Idempotent via <see cref="Tenant.ExpertReviewNotifiedAt"/>.
    /// </summary>
    Task NotifyExpertsIfNeededAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Notifie le responsable du groupe (e-mail + notification interne) qu'un compte
    /// a été créé via le lien d'invitation unique.
    /// </summary>
    Task NotifyGroupInviteLinkSignupAsync(
        Guid tenantId,
        Guid openInviteId,
        string teacherUserId,
        string teacherEmail,
        string teacherName,
        CancellationToken ct = default);
}

public sealed class ExpertReviewNotificationService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups,
    IExpertGroupManagerService managers,
    IExpertGovernanceAuditService audit,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls,
    ILogger<ExpertReviewNotificationService> logger) : IExpertReviewNotificationService
{
    public async Task NotifyExpertsIfNeededAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
            if (tenant is null)
                return;

            if (tenant.ExpertApprovalStatus != ExpertApprovalStatus.Pending)
                return;

            if (tenant.ExpertReviewNotifiedAt is not null)
                return;

            var group = tenant.ApprovedByExpertGroupId is Guid gid
                ? db.ExpertGroups.FirstOrDefault(g => g.Id == gid)
                : expertGroups.ResolveReviewerGroup(tenant.Country);
            if (group is null || !group.IsActive)
            {
                logger.LogInformation(
                    "Aucun groupe d'experts actif pour notifier la fiche {TenantId} ({Country}).",
                    tenantId, tenant.Country);
                return;
            }

            var memberIds = db.ExpertGroupMembers
                .Where(m => m.ExpertGroupId == group.Id)
                .Select(m => m.UserId)
                .Distinct()
                .ToList();

            if (memberIds.Count == 0)
            {
                logger.LogInformation(
                    "Groupe d'experts {GroupId} sans membres — pas d'e-mail pour {TenantId}.",
                    group.Id, tenantId);
                return;
            }

            var reviewUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/teachers/{tenant.Id}";
            var sent = 0;

            foreach (var userId in memberIds)
            {
                var contact = await contacts.GetAsync(userId, ct);
                if (contact is null)
                    continue;

                var firstName = contact.Value.DisplayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? contact.Value.DisplayName;

                await email.SendExpertTeacherPendingReviewAsync(
                    contact.Value.Email,
                    firstName,
                    tenant.Name,
                    tenant.Country,
                    reviewUrl,
                    ct);
                sent++;
            }

            if (sent == 0)
            {
                logger.LogInformation(
                    "Aucun e-mail expert envoyé pour {TenantId} (membres sans adresse) — flag non posé.",
                    tenantId);
                return;
            }

            tenant.ExpertReviewNotifiedAt = DateTime.UtcNow;
            tenant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Notification experts ({Sent}/{Members}) pour fiche {TenantId} → groupe {GroupName}.",
                sent, memberIds.Count, tenantId, group.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec notification experts pour fiche {TenantId}.", tenantId);
        }
    }

    public async Task NotifyGroupInviteLinkSignupAsync(
        Guid tenantId,
        Guid openInviteId,
        string teacherUserId,
        string teacherEmail,
        string teacherName,
        CancellationToken ct = default)
    {
        try
        {
            var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
            if (tenant is null)
                return;

            var groupId = tenant.ApprovedByExpertGroupId;
            if (groupId is not Guid gid)
                return;

            var group = db.ExpertGroups.FirstOrDefault(g => g.Id == gid);
            var displayName = string.IsNullOrWhiteSpace(teacherName) ? tenant.Name : teacherName.Trim();
            var emailAddr = (teacherEmail ?? "").Trim();
            var summary =
                $"Nouveau compte via le lien d’invitation unique : {displayName}" +
                (string.IsNullOrWhiteSpace(emailAddr) ? "" : $" ({emailAddr})") +
                ". Dossier à examiner.";

            await audit.RecordAsync(
                ExpertGovernanceEventType.TeacherRegisteredViaInviteLink,
                string.IsNullOrWhiteSpace(teacherUserId) ? "system" : teacherUserId,
                summary,
                expertGroupId: gid,
                relatedTenantId: tenantId,
                relatedEntityId: openInviteId,
                isNotification: true,
                ct: ct);

            var recipientIds = new HashSet<string>(StringComparer.Ordinal);
            var mandate = managers.GetActiveMandate(gid);
            if (mandate is not null)
                recipientIds.Add(mandate.UserId);

            var openInvite = db.TeacherApplicationInvites.FirstOrDefault(i => i.Id == openInviteId);
            if (openInvite is not null && !string.IsNullOrWhiteSpace(openInvite.InvitedByUserId))
                recipientIds.Add(openInvite.InvitedByUserId);

            if (recipientIds.Count == 0)
            {
                foreach (var id in db.ExpertGroupMembers
                    .Where(m => m.ExpertGroupId == gid && m.Status == ExpertMembershipStatus.Active)
                    .Select(m => m.UserId))
                    recipientIds.Add(id);
            }

            var reviewUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/approvals";
            var sent = 0;
            foreach (var userId in recipientIds)
            {
                var contact = await contacts.GetAsync(userId, ct);
                if (contact is null || string.IsNullOrWhiteSpace(contact.Value.Email))
                    continue;

                var firstName = contact.Value.DisplayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? contact.Value.DisplayName;

                await email.SendExpertTeacherPendingReviewAsync(
                    contact.Value.Email,
                    firstName,
                    $"{displayName} — inscription via lien unique",
                    string.IsNullOrWhiteSpace(emailAddr) ? "Lien d’invitation unique" : emailAddr,
                    reviewUrl,
                    ct);
                sent++;
            }

            logger.LogInformation(
                "Notification lien unique ({Sent}) pour {TenantId} ({Teacher}) → groupe {Group}.",
                sent, tenantId, displayName, group?.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec notification lien unique pour fiche {TenantId}.", tenantId);
        }
    }
}
