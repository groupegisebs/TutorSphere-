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
}

public sealed class ExpertReviewNotificationService(
    IApplicationDbContext db,
    IExpertGroupService expertGroups,
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

            var group = expertGroups.ResolveReviewerGroup(tenant.Country);
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
}
