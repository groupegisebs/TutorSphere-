using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertGroupService
{
    Task<IReadOnlyList<ExpertGroupDto>> ListAsync(CancellationToken ct = default);
    Task<ExpertGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExpertGroupDto> CreateAsync(CreateExpertGroupRequest request, CancellationToken ct = default);
    Task<ExpertGroupDto> UpdateAsync(Guid id, UpdateExpertGroupRequest request, CancellationToken ct = default);
    /// <summary>Met à jour uniquement le logo (évite les règles d'activation / pays).</summary>
    Task<ExpertGroupDto> SetLogoUrlAsync(Guid id, string? logoUrl, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task<bool> CanHardDeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertGroupMemberDto>> ListMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<ExpertGroupMemberDto> AddMemberAsync(
        Guid groupId,
        string userId,
        string? adminUserId = null,
        string? specialty = null,
        CancellationToken ct = default);
    Task RemoveMemberAsync(Guid groupId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Groupe chargé de revoir un enseignant : groupe du pays s'il existe, sinon le groupe international.
    /// </summary>
    ExpertGroup? ResolveReviewerGroup(string? teacherCountryCode);
}

public class ExpertGroupService(IApplicationDbContext db) : IExpertGroupService
{
    public Task<IReadOnlyList<ExpertGroupDto>> ListAsync(CancellationToken ct = default)
    {
        var groups = db.ExpertGroups
            .OrderByDescending(g => g.IsInternational)
            .ThenBy(g => g.CountryCode)
            .ThenBy(g => g.Name)
            .ToList();

        var memberCounts = db.ExpertGroupMembers
            .Where(m => m.Status != ExpertMembershipStatus.Removed)
            .GroupBy(m => m.ExpertGroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        var mandates = db.ExpertGroupManagerMandates
            .Where(m => m.Status == ExpertGroupManagerMandateStatus.Active)
            .AsEnumerable()
            .GroupBy(m => m.ExpertGroupId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.MandateStartsAtUtc).First());

        IReadOnlyList<ExpertGroupDto> result = groups
            .Select(g =>
            {
                mandates.TryGetValue(g.Id, out var mandate);
                return Map(g, memberCounts.GetValueOrDefault(g.Id), mandate, CanHardDelete(g.Id));
            })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<ExpertGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var g = db.ExpertGroups.FirstOrDefault(x => x.Id == id);
        if (g is null) return Task.FromResult<ExpertGroupDto?>(null);
        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id && m.Status != ExpertMembershipStatus.Removed);
        var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == id && m.Status == ExpertGroupManagerMandateStatus.Active);
        return Task.FromResult<ExpertGroupDto?>(Map(g, count, mandate, CanHardDelete(id)));
    }

    public async Task<ExpertGroupDto> CreateAsync(CreateExpertGroupRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom du groupe est requis.");

        var isInternational = request.IsInternational;
        var country = NormalizeCountry(request.CountryCode);

        if (isInternational)
        {
            if (db.ExpertGroups.Any(g => g.IsInternational))
                throw new InvalidOperationException("Un groupe international existe déjà.");
            country = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(country))
                throw new InvalidOperationException("Le code pays est requis pour un groupe national.");
            if (db.ExpertGroups.Any(g => !g.IsInternational && g.CountryCode == country))
                throw new InvalidOperationException($"Un groupe existe déjà pour le pays {country}.");
        }

        var hasManagerHint = !string.IsNullOrWhiteSpace(request.ManagerUserId)
                             || !string.IsNullOrWhiteSpace(request.ManagerEmail);
        if (!hasManagerHint)
            throw new InvalidOperationException("Un Responsable de groupe est obligatoire à la création.");

        var entity = new ExpertGroup
        {
            Name = name,
            LogoUrl = TrimOrNull(request.LogoUrl),
            Description = TrimOrNull(request.Description),
            ContactName = TrimOrNull(request.ContactName),
            ContactEmail = TrimOrNull(request.ContactEmail) ?? TrimOrNull(request.ManagerEmail),
            ContactPhone = TrimOrNull(request.ContactPhone) ?? TrimOrNull(request.ManagerPhone),
            CountryCode = country,
            IsInternational = isInternational,
            IsActive = false,
            LifecycleStatus = ExpertGroupLifecycleStatus.Draft
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, 0, null, true);
    }

    public async Task<ExpertGroupDto> UpdateAsync(Guid id, UpdateExpertGroupRequest request, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom du groupe est requis.");

        if (request.IsActive && entity.LifecycleStatus == ExpertGroupLifecycleStatus.Archived)
            throw new InvalidOperationException("Impossible de réactiver un groupe archivé. Créez un nouveau groupe ou contactez le support.");

        // Réconcilie le pointeur dénormalisé avec le mandat Active réel.
        var activeMandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == id && m.Status == ExpertGroupManagerMandateStatus.Active);
        entity.ActiveManagerMandateId = activeMandate?.Id;
        entity.GroupManagerMembershipId = activeMandate?.MembershipId;

        if (request.IsActive && activeMandate is null)
            throw new InvalidOperationException("Impossible d'activer un groupe sans Responsable actif.");

        entity.Name = name;
        entity.Description = TrimOrNull(request.Description) ?? entity.Description;
        if (request.ContactName is not null)
            entity.ContactName = TrimOrNull(request.ContactName);
        if (request.ContactEmail is not null)
            entity.ContactEmail = TrimOrNull(request.ContactEmail);
        if (request.ContactPhone is not null)
            entity.ContactPhone = TrimOrNull(request.ContactPhone);
        // LogoUrl: null = ne pas toucher ; "" = effacer ; valeur = remplacer.
        if (request.LogoUrl is not null)
            entity.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        entity.IsActive = request.IsActive;

        // Country can change for national groups only (international stays without country).
        if (!entity.IsInternational && request.CountryCode is not null)
        {
            var country = NormalizeCountry(request.CountryCode);
            if (country is null)
                throw new InvalidOperationException("Le code pays est requis pour un groupe national.");
            if (country != entity.CountryCode
                && db.ExpertGroups.Any(g => g.Id != id && !g.IsInternational && g.CountryCode == country))
            {
                throw new InvalidOperationException(
                    $"Un groupe national existe déjà pour le pays {country}.");
            }

            entity.CountryCode = country;
        }

        if (request.IsActive)
            entity.LifecycleStatus = ExpertGroupLifecycleStatus.Active;
        else if (entity.LifecycleStatus == ExpertGroupLifecycleStatus.Active)
            entity.LifecycleStatus = ExpertGroupLifecycleStatus.Suspended;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id && m.Status != ExpertMembershipStatus.Removed);
        return Map(entity, count, activeMandate, CanHardDelete(id));
    }

    public async Task<ExpertGroupDto> SetLogoUrlAsync(Guid id, string? logoUrl, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        entity.LogoUrl = TrimOrNull(logoUrl);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id && m.Status != ExpertMembershipStatus.Removed);
        var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == id && m.Status == ExpertGroupManagerMandateStatus.Active);
        return Map(entity, count, mandate, CanHardDelete(id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!CanHardDelete(id))
            throw new InvalidOperationException(
                "Ce groupe possède déjà des membres, enseignants, offres ou historiques. Utilisez Archiver.");

        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var members = db.ExpertGroupMembers.Where(m => m.ExpertGroupId == id).ToList();
        foreach (var m in members)
            db.Remove(m);

        var mandates = db.ExpertGroupManagerMandates.Where(m => m.ExpertGroupId == id).ToList();
        foreach (var m in mandates)
            db.Remove(m);

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var active = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == id && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (active is not null)
        {
            active.Status = ExpertGroupManagerMandateStatus.Ended;
            active.MandateEndsAtUtc = DateTime.UtcNow;
            active.EndReason = "Groupe archivé";
            active.UpdatedAt = DateTime.UtcNow;

            var member = db.ExpertGroupMembers.FirstOrDefault(m => m.Id == active.MembershipId);
            if (member is not null && member.MemberRole == ExpertGroupMemberRole.Manager)
            {
                member.MemberRole = ExpertGroupMemberRole.Expert;
                member.UpdatedAt = DateTime.UtcNow;
            }
        }

        entity.LifecycleStatus = ExpertGroupLifecycleStatus.Archived;
        entity.IsActive = false;
        entity.ActiveManagerMandateId = null;
        entity.GroupManagerMembershipId = null;
        entity.ManagerAssignedAtUtc = null;
        entity.ManagerAssignedByAdminId = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> CanHardDeleteAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(CanHardDelete(id));

    public Task<IReadOnlyList<ExpertGroupMemberDto>> ListMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!db.ExpertGroups.Any(g => g.Id == groupId))
            throw new InvalidOperationException("Groupe d'experts introuvable.");

        IReadOnlyList<ExpertGroupMemberDto> result = db.ExpertGroupMembers
            .Where(m => m.ExpertGroupId == groupId && m.Status != ExpertMembershipStatus.Removed)
            .OrderByDescending(m => m.MemberRole == ExpertGroupMemberRole.Manager)
            .ThenBy(m => m.CreatedAt)
            .ToList()
            .Select(m => new ExpertGroupMemberDto(
                m.Id, m.ExpertGroupId, m.UserId, string.Empty, string.Empty,
                false, false, false, m.MemberRole, m.Status, m.Specialty))
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<ExpertGroupMemberDto> AddMemberAsync(
        Guid groupId,
        string userId,
        string? adminUserId = null,
        string? specialty = null,
        CancellationToken ct = default)
    {
        if (!db.ExpertGroups.Any(g => g.Id == groupId))
            throw new InvalidOperationException("Groupe d'experts introuvable.");
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Utilisateur requis.");

        var trimmedUserId = userId.Trim();

        if (db.ExpertGroupMembers.Any(m =>
                m.ExpertGroupId == groupId
                && m.UserId == trimmedUserId
                && m.Status != ExpertMembershipStatus.Removed))
            throw new InvalidOperationException("Cet utilisateur est déjà membre du groupe.");

        var otherGroupId = db.ExpertGroupMembers
            .Where(m => m.UserId == trimmedUserId
                        && m.ExpertGroupId != groupId
                        && m.Status != ExpertMembershipStatus.Removed)
            .Select(m => m.ExpertGroupId)
            .FirstOrDefault();
        if (otherGroupId != Guid.Empty)
        {
            var otherGroupName = db.ExpertGroups.FirstOrDefault(g => g.Id == otherGroupId)?.Name ?? "un autre groupe";
            throw new InvalidOperationException(
                $"Cet utilisateur appartient déjà au groupe « {otherGroupName} ». " +
                "Un expert ne peut appartenir qu'à un seul groupe : retirez-le de son groupe actuel avant de l'ajouter ici.");
        }

        var existingRemoved = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.ExpertGroupId == groupId && m.UserId == trimmedUserId);
        if (existingRemoved is not null)
        {
            existingRemoved.Status = ExpertMembershipStatus.Active;
            existingRemoved.AdmissionMethod = ExpertAdmissionMethod.AdminDirect;
            existingRemoved.MemberRole = ExpertGroupMemberRole.Expert;
            existingRemoved.Specialty = string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim();
            existingRemoved.AdmittedAtUtc = DateTime.UtcNow;
            existingRemoved.EndedAtUtc = null;
            existingRemoved.ApprovedByAdminId = adminUserId;
            existingRemoved.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new ExpertGroupMemberDto(
                existingRemoved.Id, groupId, existingRemoved.UserId, string.Empty, string.Empty,
                MemberRole: existingRemoved.MemberRole, Status: existingRemoved.Status,
                Specialty: existingRemoved.Specialty);
        }

        var member = new ExpertGroupMember
        {
            ExpertGroupId = groupId,
            UserId = trimmedUserId,
            Status = ExpertMembershipStatus.Active,
            AdmissionMethod = ExpertAdmissionMethod.AdminDirect,
            MemberRole = ExpertGroupMemberRole.Expert,
            Specialty = string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim(),
            AdmittedAtUtc = DateTime.UtcNow,
            ApprovedByAdminId = adminUserId
        };
        db.Add(member);
        await db.SaveChangesAsync(ct);
        return new ExpertGroupMemberDto(
            member.Id, groupId, member.UserId, string.Empty, string.Empty,
            MemberRole: member.MemberRole, Status: member.Status, Specialty: member.Specialty);
    }

    public async Task RemoveMemberAsync(Guid groupId, string userId, CancellationToken ct = default)
    {
        var member = db.ExpertGroupMembers.FirstOrDefault(m => m.ExpertGroupId == groupId && m.UserId == userId)
            ?? throw new InvalidOperationException("Membre introuvable.");

        var isActiveManager = db.ExpertGroupManagerMandates.Any(m =>
            m.ExpertGroupId == groupId
            && m.UserId == userId
            && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (isActiveManager || member.MemberRole == ExpertGroupMemberRole.Manager)
            throw new InvalidOperationException(
                "Impossible de retirer le Responsable actif. Transférez d'abord la responsabilité.");

        member.Status = ExpertMembershipStatus.Removed;
        member.EndedAtUtc = DateTime.UtcNow;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public ExpertGroup? ResolveReviewerGroup(string? teacherCountryCode)
    {
        var country = NormalizeCountry(teacherCountryCode);
        if (!string.IsNullOrWhiteSpace(country))
        {
            var national = db.ExpertGroups.FirstOrDefault(g =>
                g.IsActive && !g.IsInternational && g.CountryCode == country);
            if (national is not null)
                return national;
        }

        return db.ExpertGroups.FirstOrDefault(g => g.IsActive && g.IsInternational);
    }

    private bool CanHardDelete(Guid id)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == id);
        var isDraft = group?.LifecycleStatus == ExpertGroupLifecycleStatus.Draft;

        // Membres/mandats d'un brouillon (création partielle) n'empêchent pas la suppression compensatoire.
        if (!isDraft && db.ExpertGroupMembers.Any(m => m.ExpertGroupId == id && m.Status != ExpertMembershipStatus.Removed))
            return false;
        if (db.TeacherApplicationInvites.Any(i => i.ExpertGroupId == id))
            return false;
        if (db.ExpertMembershipInvites.Any(i => i.ExpertGroupId == id))
            return false;
        if (db.GroupOffers.Any(o => o.ExpertGroupId == id))
            return false;
        if (db.GroupAdminConversations.Any(c => c.ExpertGroupId == id))
            return false;
        if (db.Disciplines.Any(d => d.ExpertGroupId == id))
            return false;
        if (db.Tenants.Any(t => t.ApprovedByExpertGroupId == id))
            return false;
        if (db.ExpertDelegatedTasks.Any(t => t.ExpertGroupId == id))
            return false;
        if (db.ExpertWorkspaceItems.Any(w => w.ExpertGroupId == id))
            return false;
        if (db.ExpertGovernanceEvents.Any(e => e.ExpertGroupId == id))
            return false;
        return true;
    }

    private static ExpertGroupDto Map(
        ExpertGroup g,
        int memberCount,
        ExpertGroupManagerMandate? mandate,
        bool canHardDelete) =>
        new(g.Id, g.Name, g.LogoUrl, g.ContactName, g.ContactEmail, g.ContactPhone,
            g.CountryCode, g.IsInternational, g.IsActive, memberCount, g.CreatedAt,
            g.Description, g.LifecycleStatus,
            ActiveManagerMandateId: mandate?.Id ?? g.ActiveManagerMandateId,
            ManagerPhone: mandate?.Phone ?? g.ContactPhone,
            ManagerUserId: mandate?.UserId,
            ManagerFullName: g.ContactName,
            ManagerEmail: g.ContactEmail,
            CanHardDelete: canHardDelete);

    private static string? NormalizeCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return code.Trim().ToUpperInvariant();
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
