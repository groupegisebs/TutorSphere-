using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertGroupManagerService
{
    bool IsActiveManager(string userId, Guid? groupId = null);
    ExpertGroupManagerMandate? GetActiveMandate(Guid groupId);
    Task<ExpertGroupManagerDto?> GetActiveManagerAsync(Guid groupId, CancellationToken ct = default);
    Task AppointAsync(Guid groupId, string adminUserId, string membershipUserId, AppointGroupManagerRequest extras, CancellationToken ct = default);
    Task EndActiveMandateAsync(Guid groupId, string adminUserId, string? reason, CancellationToken ct = default);
    Task SuspendActiveMandateAsync(Guid groupId, string adminUserId, string? reason, CancellationToken ct = default);
}

public class ExpertGroupManagerService(IApplicationDbContext db) : IExpertGroupManagerService
{
    public bool IsActiveManager(string userId, Guid? groupId = null)
    {
        var q = db.ExpertGroupManagerMandates.Where(m =>
            m.UserId == userId && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (groupId is Guid g)
            q = q.Where(m => m.ExpertGroupId == g);
        return q.Any();
    }

    public ExpertGroupManagerMandate? GetActiveMandate(Guid groupId) =>
        db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == groupId && m.Status == ExpertGroupManagerMandateStatus.Active);

    public Task<ExpertGroupManagerDto?> GetActiveManagerAsync(Guid groupId, CancellationToken ct = default)
    {
        var mandate = GetActiveMandate(groupId);
        if (mandate is null) return Task.FromResult<ExpertGroupManagerDto?>(null);
        return Task.FromResult<ExpertGroupManagerDto?>(new ExpertGroupManagerDto(
            mandate.Id,
            mandate.MembershipId,
            mandate.UserId,
            string.Empty,
            string.Empty,
            mandate.Phone,
            mandate.FunctionTitle,
            mandate.Status,
            mandate.MandateStartsAtUtc,
            mandate.MandateEndsAtUtc,
            mandate.IsTemporary));
    }

    public async Task AppointAsync(
        Guid groupId,
        string adminUserId,
        string membershipUserId,
        AppointGroupManagerRequest extras,
        CancellationToken ct = default)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");

        var member = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.ExpertGroupId == groupId
            && m.UserId == membershipUserId
            && m.Status == ExpertMembershipStatus.Active)
            ?? throw new InvalidOperationException("Le Responsable doit être un membre actif du groupe.");

        await EndActiveMandateAsync(groupId, adminUserId, "Remplacé par un nouveau Responsable.", ct);

        // Reset previous manager roles in group
        foreach (var previous in db.ExpertGroupMembers.Where(m =>
                     m.ExpertGroupId == groupId && m.MemberRole == ExpertGroupMemberRole.Manager))
        {
            previous.MemberRole = ExpertGroupMemberRole.Expert;
            previous.UpdatedAt = DateTime.UtcNow;
        }

        member.MemberRole = ExpertGroupMemberRole.Manager;
        member.UpdatedAt = DateTime.UtcNow;

        var starts = extras.MandateStartsAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var mandate = new ExpertGroupManagerMandate
        {
            ExpertGroupId = groupId,
            MembershipId = member.Id,
            UserId = membershipUserId,
            Status = ExpertGroupManagerMandateStatus.Active,
            FunctionTitle = string.IsNullOrWhiteSpace(extras.FunctionTitle) ? null : extras.FunctionTitle.Trim(),
            Phone = string.IsNullOrWhiteSpace(extras.Phone) ? null : extras.Phone.Trim(),
            MandateStartsAtUtc = starts,
            AppointedByAdminId = adminUserId,
            IsTemporary = extras.IsTemporary
        };
        db.Add(mandate);
        await db.SaveChangesAsync(ct);

        group.ActiveManagerMandateId = mandate.Id;
        group.GroupManagerMembershipId = member.Id;
        group.ManagerAssignedAtUtc = starts;
        group.ManagerAssignedByAdminId = adminUserId;
        var contactName = string.Join(' ', new[] { extras.FirstName, extras.LastName }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim()));
        if (!string.IsNullOrWhiteSpace(contactName))
            group.ContactName = contactName;
        group.ContactPhone = mandate.Phone ?? group.ContactPhone;
        group.ContactEmail = string.IsNullOrWhiteSpace(extras.Email) ? group.ContactEmail : extras.Email.Trim();
        if (group.LifecycleStatus == ExpertGroupLifecycleStatus.Draft)
            group.LifecycleStatus = ExpertGroupLifecycleStatus.Active;
        group.IsActive = group.LifecycleStatus == ExpertGroupLifecycleStatus.Active;
        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task EndActiveMandateAsync(Guid groupId, string adminUserId, string? reason, CancellationToken ct = default)
    {
        var active = GetActiveMandate(groupId);
        if (active is null) return;

        active.Status = ExpertGroupManagerMandateStatus.Ended;
        active.MandateEndsAtUtc = DateTime.UtcNow;
        active.EndedByAdminId = adminUserId;
        active.EndReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        active.UpdatedAt = DateTime.UtcNow;

        var member = db.ExpertGroupMembers.FirstOrDefault(m => m.Id == active.MembershipId);
        if (member is not null && member.MemberRole == ExpertGroupMemberRole.Manager)
        {
            member.MemberRole = ExpertGroupMemberRole.Expert;
            member.UpdatedAt = DateTime.UtcNow;
        }

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is not null && group.ActiveManagerMandateId == active.Id)
        {
            group.ActiveManagerMandateId = null;
            group.GroupManagerMembershipId = null;
            group.ManagerAssignedAtUtc = null;
            group.ManagerAssignedByAdminId = null;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task SuspendActiveMandateAsync(Guid groupId, string adminUserId, string? reason, CancellationToken ct = default)
    {
        var active = GetActiveMandate(groupId)
            ?? throw new InvalidOperationException("Aucun Responsable actif.");

        active.Status = ExpertGroupManagerMandateStatus.Suspended;
        active.MandateEndsAtUtc = DateTime.UtcNow;
        active.EndedByAdminId = adminUserId;
        active.EndReason = string.IsNullOrWhiteSpace(reason) ? "Suspension administrative" : reason.Trim();
        active.UpdatedAt = DateTime.UtcNow;

        var member = db.ExpertGroupMembers.FirstOrDefault(m => m.Id == active.MembershipId);
        if (member is not null && member.MemberRole == ExpertGroupMemberRole.Manager)
        {
            member.MemberRole = ExpertGroupMemberRole.Expert;
            member.UpdatedAt = DateTime.UtcNow;
        }

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is not null)
        {
            group.ActiveManagerMandateId = null;
            group.GroupManagerMembershipId = null;
            group.ManagerAssignedAtUtc = null;
            group.ManagerAssignedByAdminId = null;
            group.LifecycleStatus = ExpertGroupLifecycleStatus.Suspended;
            group.IsActive = false;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}