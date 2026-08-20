using TutorSphere.Application.Common;
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
    Task<ExpertGroupDto> SetBannerUrlAsync(Guid id, string? bannerUrl, CancellationToken ct = default);
    Task<ExpertGroupDto> SetBrandColorsAsync(Guid id, string? primaryColor, string? secondaryColor, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Suppression définitive, y compris les données rattachées. Réservée aux cas où l'archivage
    /// ne convient pas : un groupe créé par erreur, un doublon. L'appelant doit avoir montré
    /// <see cref="GetDeletionImpactAsync"/> à l'utilisateur avant d'appeler ceci.
    /// </summary>
    Task DeleteCascadeAsync(Guid id, CancellationToken ct = default);

    /// <summary>Inventaire de ce que la suppression détruirait, pour écrire la confirmation.</summary>
    Task<ExpertGroupDeletionImpactDto> GetDeletionImpactAsync(Guid id, CancellationToken ct = default);

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
            if (InternationalSlotTaken())
                throw new InvalidOperationException("Un groupe international actif existe déjà.");
            country = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(country))
                throw new InvalidOperationException("Le code pays est requis pour un groupe national.");
            if (NationalSlotTaken(country))
                throw new InvalidOperationException($"Un groupe national actif existe déjà pour le pays {country}.");
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

        if (entity.LifecycleStatus == ExpertGroupLifecycleStatus.Archived && request.IsActive)
            throw new InvalidOperationException("Impossible de réactiver un groupe archivé. Créez un nouveau groupe ou contactez le support.");

        // Réconcilie le pointeur dénormalisé avec le mandat Active réel.
        var activeMandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == id && m.Status == ExpertGroupManagerMandateStatus.Active);
        entity.ActiveManagerMandateId = activeMandate?.Id;
        entity.GroupManagerMembershipId = activeMandate?.MembershipId;

        if (request.IsActive && activeMandate is null)
            throw new InvalidOperationException("Impossible d'activer un groupe sans Responsable actif.");

        // Ne pas réactiver silencieusement un groupe Suspended via un simple IsActive=true
        // sans transition explicite depuis Suspended → Active (autorisée ici si mandat présent).
        if (request.IsActive
            && entity.LifecycleStatus == ExpertGroupLifecycleStatus.Suspended
            && activeMandate is null)
            throw new InvalidOperationException("Impossible de réactiver un groupe suspendu sans Responsable actif.");

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

        var wasActive = entity.IsActive;
        entity.IsActive = request.IsActive;

        // Country can change for national groups only (international stays without country).
        if (!entity.IsInternational && request.CountryCode is not null)
        {
            var country = NormalizeCountry(request.CountryCode);
            if (country is null)
                throw new InvalidOperationException("Le code pays est requis pour un groupe national.");
            entity.CountryCode = country;
        }

        if (request.IsActive)
        {
            EnsureActiveTerritoryAvailable(entity, id);
            entity.LifecycleStatus = ExpertGroupLifecycleStatus.Active;
        }
        else if (wasActive || entity.LifecycleStatus == ExpertGroupLifecycleStatus.Active)
        {
            // Soft-deactivate = Suspended + même nettoyage mandat qu'archive (sans hard-delete).
            entity.LifecycleStatus = ExpertGroupLifecycleStatus.Suspended;
            EndActiveMandateForGroup(entity, "Groupe désactivé (soft)");
            activeMandate = null;
        }

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
        return MapCurrent(entity);
    }

    public async Task<ExpertGroupDto> SetBannerUrlAsync(Guid id, string? bannerUrl, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        entity.BannerUrl = TrimOrNull(bannerUrl);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapCurrent(entity);
    }

    public async Task<ExpertGroupDto> SetBrandColorsAsync(Guid id, string? primaryColor, string? secondaryColor, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        if (primaryColor is not null)
            entity.PrimaryColor = ColorHex.NormalizeOrNull(primaryColor);
        if (secondaryColor is not null)
            entity.SecondaryColor = ColorHex.NormalizeOrNull(secondaryColor);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapCurrent(entity);
    }

    private ExpertGroupDto MapCurrent(ExpertGroup entity)
    {
        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == entity.Id && m.Status != ExpertMembershipStatus.Removed);
        var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == entity.Id && m.Status == ExpertGroupManagerMandateStatus.Active);
        return Map(entity, count, mandate, CanHardDelete(entity.Id));
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

    public Task<ExpertGroupDeletionImpactDto> GetDeletionImpactAsync(Guid id, CancellationToken ct = default)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        List<ExpertGroupDeletionItemDto> deleted =
        [
            new("Membres du groupe", db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id)),
            new("Mandats de Responsable", db.ExpertGroupManagerMandates.Count(m => m.ExpertGroupId == id)),
            new("Rôles définis", db.ExpertGroupDefinedRoles.Count(r => r.ExpertGroupId == id)),
            new("Offres de groupe", db.GroupOffers.Count(o => o.ExpertGroupId == id)),
            new("Conversations avec l'administration", db.GroupAdminConversations.Count(c => c.ExpertGroupId == id)),
            new("Disciplines et services associés", db.Disciplines.Count(d => d.ExpertGroupId == id)),
            new("Invitations d'enseignants", db.TeacherApplicationInvites.Count(i => i.ExpertGroupId == id)),
            new("Invitations d'experts et votes", db.ExpertMembershipInvites.Count(i => i.ExpertGroupId == id)),
            new("Tâches déléguées", db.ExpertDelegatedTasks.Count(t => t.ExpertGroupId == id)),
            new("Éléments d'espace de travail", db.ExpertWorkspaceItems.Count(w => w.ExpertGroupId == id)),
            new("Événements de gouvernance", db.ExpertGovernanceEvents.Count(e => e.ExpertGroupId == id)),
            new("Contrats d'enseignants", db.TeacherContracts.Count(c => c.ExpertGroupId == id)),
            new("Rattachements à des réunions", db.MeetingGroups.Count(m => m.ExpertGroupId == id)),
            new("Remplacements de cours", db.LessonCoverageAssignments.Count(c => c.ExpertGroupId == id))
        ];

        List<ExpertGroupDeletionItemDto> detached =
        [
            new("Écoles approuvées par ce groupe", db.Tenants.Count(t => t.ApprovedByExpertGroupId == id)),
            new("Réunions organisées", db.Meetings.Count(m => m.OrganizerGroupId == id)),
            new("Candidatures orientées vers ce groupe", db.TeacherInterestRequests.Count(r => r.RoutedExpertGroupId == id)),
            new("Remarques d'experts", db.ExpertRemarksForAnyTenant.Count(r => r.ExpertGroupId == id))
        ];

        return Task.FromResult(new ExpertGroupDeletionImpactDto(
            group.Id,
            group.Name,
            group.IsActive,
            group.LifecycleStatus,
            [.. deleted.Where(d => d.Count > 0)],
            [.. detached.Where(d => d.Count > 0)]));
    }

    public async Task DeleteCascadeAsync(Guid id, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        // Les rattachements sont dénoués avant les suppressions : une école ou une réunion ne
        // disparaît pas avec le groupe qui l'a approuvée ou organisée.
        foreach (var tenant in db.Tenants.Where(t => t.ApprovedByExpertGroupId == id).ToList())
            tenant.ApprovedByExpertGroupId = null;
        foreach (var meeting in db.Meetings.Where(m => m.OrganizerGroupId == id).ToList())
            meeting.OrganizerGroupId = null;
        foreach (var request in db.TeacherInterestRequests.Where(r => r.RoutedExpertGroupId == id).ToList())
            request.RoutedExpertGroupId = null;
        foreach (var remark in db.ExpertRemarksForAnyTenant.Where(r => r.ExpertGroupId == id).ToList())
            remark.ExpertGroupId = null;

        // Suppressions explicites plutôt que cascade de la base : l'ordre reste lisible, et le
        // comportement est le même quel que soit le fournisseur derrière le contexte.
        var contractIds = db.TeacherContracts.Where(c => c.ExpertGroupId == id).Select(c => c.Id).ToList();
        if (contractIds.Count > 0)
        {
            db.RemoveRange(db.TeacherContractSectionDecisions.Where(s => contractIds.Contains(s.ContractId)).ToList());
            db.RemoveRange(db.TeacherContractAuditEvents.Where(a => contractIds.Contains(a.ContractId)).ToList());
            db.RemoveRange(db.TeacherContracts.Where(c => contractIds.Contains(c.Id)).ToList());
        }

        var offerIds = db.GroupOffers.Where(o => o.ExpertGroupId == id).Select(o => o.Id).ToList();
        if (offerIds.Count > 0)
        {
            db.RemoveRange(db.GroupOfferTeachers.Where(t => offerIds.Contains(t.GroupOfferId)).ToList());
            db.RemoveRange(db.GroupOffers.Where(o => offerIds.Contains(o.Id)).ToList());
        }

        var conversationIds = db.GroupAdminConversations.Where(c => c.ExpertGroupId == id).Select(c => c.Id).ToList();
        if (conversationIds.Count > 0)
        {
            db.RemoveRange(db.GroupAdminMessages.Where(m => conversationIds.Contains(m.ConversationId)).ToList());
            db.RemoveRange(db.GroupAdminConversations.Where(c => conversationIds.Contains(c.Id)).ToList());
        }

        var disciplineIds = db.Disciplines.Where(d => d.ExpertGroupId == id).Select(d => d.Id).ToList();
        if (disciplineIds.Count > 0)
        {
            db.RemoveRange(db.DisciplineServiceItems.Where(s => disciplineIds.Contains(s.DisciplineId)).ToList());
            db.RemoveRange(db.TeacherDisciplineAssignments.Where(a => disciplineIds.Contains(a.DisciplineId)).ToList());
            db.RemoveRange(db.Disciplines.Where(d => disciplineIds.Contains(d.Id)).ToList());
        }

        var inviteIds = db.ExpertMembershipInvites.Where(i => i.ExpertGroupId == id).Select(i => i.Id).ToList();
        if (inviteIds.Count > 0)
        {
            db.RemoveRange(db.ExpertMembershipVotes.Where(v => inviteIds.Contains(v.InviteId)).ToList());
            db.RemoveRange(db.ExpertMembershipInvites.Where(i => inviteIds.Contains(i.Id)).ToList());
        }

        db.RemoveRange(db.TeacherApplicationInvites.Where(i => i.ExpertGroupId == id).ToList());
        db.RemoveRange(db.ExpertDelegatedTasks.Where(t => t.ExpertGroupId == id).ToList());
        db.RemoveRange(db.ExpertWorkspaceItems.Where(w => w.ExpertGroupId == id).ToList());
        db.RemoveRange(db.ExpertGovernanceEvents.Where(e => e.ExpertGroupId == id).ToList());
        db.RemoveRange(db.MeetingGroups.Where(m => m.ExpertGroupId == id).ToList());
        db.RemoveRange(db.LessonCoverageAssignments.Where(c => c.ExpertGroupId == id).ToList());
        db.RemoveRange(db.ExpertGroupDefinedRoles.Where(r => r.ExpertGroupId == id).ToList());

        // Le pointeur dénormalisé du groupe référence un mandat et une adhésion : il doit tomber
        // avant eux, sinon la contrainte du mandat vers l'adhésion bloque la suppression.
        entity.ActiveManagerMandateId = null;
        entity.GroupManagerMembershipId = null;
        db.RemoveRange(db.ExpertGroupManagerMandates.Where(m => m.ExpertGroupId == id).ToList());
        db.RemoveRange(db.ExpertGroupMembers.Where(m => m.ExpertGroupId == id).ToList());

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        EndActiveMandateForGroup(entity, "Groupe archivé");

        entity.LifecycleStatus = ExpertGroupLifecycleStatus.Archived;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Termine le mandat Active et rétrograde le membre Manager → Expert (sans toucher Identity).
    /// </summary>
    private void EndActiveMandateForGroup(ExpertGroup entity, string reason)
    {
        var active = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.ExpertGroupId == entity.Id && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (active is not null)
        {
            active.Status = ExpertGroupManagerMandateStatus.Ended;
            active.MandateEndsAtUtc = DateTime.UtcNow;
            active.EndReason = reason;
            active.UpdatedAt = DateTime.UtcNow;

            var member = db.ExpertGroupMembers.FirstOrDefault(m => m.Id == active.MembershipId);
            if (member is not null && member.MemberRole == ExpertGroupMemberRole.Manager)
            {
                member.MemberRole = ExpertGroupMemberRole.Expert;
                member.UpdatedAt = DateTime.UtcNow;
            }
        }

        entity.ActiveManagerMandateId = null;
        entity.GroupManagerMembershipId = null;
        entity.ManagerAssignedAtUtc = null;
        entity.ManagerAssignedByAdminId = null;
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
            BannerUrl: g.BannerUrl,
            PrimaryColor: g.PrimaryColor,
            SecondaryColor: g.SecondaryColor,
            CanHardDelete: canHardDelete);

    private bool NationalSlotTaken(string country, Guid? exceptId = null) =>
        db.ExpertGroups.Any(g =>
            g.IsActive
            && !g.IsInternational
            && g.CountryCode == country
            && (exceptId == null || g.Id != exceptId));

    private bool InternationalSlotTaken(Guid? exceptId = null) =>
        db.ExpertGroups.Any(g =>
            g.IsActive
            && g.IsInternational
            && (exceptId == null || g.Id != exceptId));

    private void EnsureActiveTerritoryAvailable(ExpertGroup entity, Guid exceptId)
    {
        if (entity.IsInternational)
        {
            if (InternationalSlotTaken(exceptId))
                throw new InvalidOperationException("Un groupe international actif existe déjà.");
            return;
        }

        var country = entity.CountryCode;
        if (string.IsNullOrWhiteSpace(country))
            throw new InvalidOperationException("Le code pays est requis pour un groupe national.");
        if (NationalSlotTaken(country, exceptId))
            throw new InvalidOperationException($"Un groupe national actif existe déjà pour le pays {country}.");
    }

    private static string? NormalizeCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return code.Trim().ToUpperInvariant();
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
