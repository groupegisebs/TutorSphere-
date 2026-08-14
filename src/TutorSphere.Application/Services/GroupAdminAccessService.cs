using System.Security.Claims;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

/// <summary>
/// Résout le groupe administré : Responsable actif (mandat), ou SuperAdmin/PlatformAdmin en mode suppléant
/// (passer <paramref name="actAsGroupId"/> depuis le header <c>X-Act-As-Expert-Group-Id</c>).
/// </summary>
public interface IGroupAdminAccessService
{
    public const string ActAsHeaderName = "X-Act-As-Expert-Group-Id";

    bool IsPlatformAdmin(ClaimsPrincipal user);

    Task<ExpertMyGroupDto?> ResolveManagedGroupAsync(
        ClaimsPrincipal user,
        Guid? actAsGroupId,
        CancellationToken ct = default);

    Task<Guid> RequireManagedGroupIdAsync(
        ClaimsPrincipal user,
        Guid? actAsGroupId,
        CancellationToken ct = default);
}

public sealed class GroupAdminAccessService(
    IApplicationDbContext db,
    IExpertGroupManagerService managers) : IGroupAdminAccessService
{
    public bool IsPlatformAdmin(ClaimsPrincipal user) =>
        user.IsInRole(UserRoles.SuperAdmin) || user.IsInRole(UserRoles.PlatformAdmin);

    public Task<ExpertMyGroupDto?> ResolveManagedGroupAsync(
        ClaimsPrincipal user,
        Guid? actAsGroupId,
        CancellationToken ct = default)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult<ExpertMyGroupDto?>(null);

        if (IsPlatformAdmin(user) && actAsGroupId is Guid gid)
        {
            var group = db.ExpertGroups.FirstOrDefault(g => g.Id == gid && g.IsActive);
            if (group is null) return Task.FromResult<ExpertMyGroupDto?>(null);
            return Task.FromResult<ExpertMyGroupDto?>(new ExpertMyGroupDto(
                group.Id, group.Name, group.CountryCode, group.Description, group.IsInternational));
        }

        // Mandat Active uniquement — un rôle Identity orphelin ne suffit plus.
        if (!managers.IsActiveManager(userId))
            return Task.FromResult<ExpertMyGroupDto?>(null);

        var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.UserId == userId && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (mandate is null) return Task.FromResult<ExpertMyGroupDto?>(null);

        var managed = db.ExpertGroups.FirstOrDefault(g => g.Id == mandate.ExpertGroupId && g.IsActive);
        if (managed is null) return Task.FromResult<ExpertMyGroupDto?>(null);
        return Task.FromResult<ExpertMyGroupDto?>(new ExpertMyGroupDto(
            managed.Id, managed.Name, managed.CountryCode, managed.Description, managed.IsInternational));
    }

    public async Task<Guid> RequireManagedGroupIdAsync(
        ClaimsPrincipal user,
        Guid? actAsGroupId,
        CancellationToken ct = default)
    {
        var group = await ResolveManagedGroupAsync(user, actAsGroupId, ct)
            ?? throw new InvalidOperationException(
                "Accès réservé au Responsable du groupe (ou administrateur plateforme en mode suppléant).");
        return group.Id;
    }
}
