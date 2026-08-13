using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Admin;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITeacherSchoolAdminService
{
    Task<TeacherSchoolRecordDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TeacherSchoolRecordDto?> GetByOwnerUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Met à jour le profil école (tenant). Les champs user (prénom/nom/tél) sont mis à jour via le callback.</summary>
    Task<TeacherSchoolRecordDto> UpdateTenantProfileAsync(
        Guid tenantId,
        UpdateTeacherSchoolRecordRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Publie la fiche publique (même critères que la recherche / page /school/{slug}) :
    /// Active + IsPublicProfile + Approuvé + Onboarding + Licence.
    /// </summary>
    Task<PublishTeacherPublicProfileResult> PublishPublicProfileAsync(
        Guid tenantId,
        string actorUserId,
        bool asPlatformAdmin,
        CancellationToken ct = default);

    /// <summary>Retire la fiche de la recherche / page publique (IsPublicProfile = false).</summary>
    Task<PublishTeacherPublicProfileResult> UnpublishPublicProfileAsync(
        Guid tenantId,
        string actorUserId,
        bool asPlatformAdmin,
        CancellationToken ct = default);

    void EnsureExpertCanManageTeacher(Guid tenantId, string expertUserId);
}

public sealed class TeacherSchoolAdminService(
    IApplicationDbContext db,
    IUserContactLookup contacts) : ITeacherSchoolAdminService
{
    public async Task<TeacherSchoolRecordDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId);
        return tenant is null ? null : await MapAsync(tenant, ct);
    }

    public async Task<TeacherSchoolRecordDto?> GetByOwnerUserIdAsync(string userId, CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == userId);
        return tenant is null ? null : await MapAsync(tenant, ct);
    }

    public async Task<TeacherSchoolRecordDto> UpdateTenantProfileAsync(
        Guid tenantId,
        UpdateTeacherSchoolRecordRequest request,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École / enseignant introuvable.");

        if (!string.IsNullOrWhiteSpace(request.SchoolName))
            tenant.Name = request.SchoolName.Trim();
        if (request.Description is not null)
            tenant.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        if (request.City is not null)
            tenant.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        if (request.Country is not null)
        {
            var c = ProfileVisibility.NormalizeCode(request.Country);
            tenant.Country = c.Length == 2 ? c : request.Country.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.Language))
            tenant.Language = request.Language.Trim();
        if (!string.IsNullOrWhiteSpace(request.Currency))
            tenant.Currency = request.Currency.Trim();

        if (request.VisibleCountryCodes is not null)
            tenant.VisibleCountryCodes = ProfileVisibility.ToCsv(request.VisibleCountryCodes, tenant.Country);
        else if (request.Country is not null && string.IsNullOrWhiteSpace(tenant.VisibleCountryCodes))
            tenant.VisibleCountryCodes = ProfileVisibility.ToCsv(null, tenant.Country);

        // Page publique /school/{slug} affiche Branding.Presentation en priorité, sinon Description.
        if (request.Presentation is not null || request.Description is not null)
        {
            var branding = db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);
            if (branding is null)
            {
                branding = new TenantBranding { TenantId = tenant.Id };
                db.Add(branding);
            }

            if (request.Presentation is not null)
            {
                branding.Presentation = string.IsNullOrWhiteSpace(request.Presentation)
                    ? null
                    : request.Presentation.Trim();
            }
            else if (request.Description is not null
                     && string.IsNullOrWhiteSpace(branding.Presentation))
            {
                // Première rédaction : propager la description vers la présentation publique.
                branding.Presentation = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim();
            }

            branding.UpdatedAt = DateTime.UtcNow;
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await MapAsync(tenant, ct))!;
    }

    public async Task<PublishTeacherPublicProfileResult> PublishPublicProfileAsync(
        Guid tenantId,
        string actorUserId,
        bool asPlatformAdmin,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École / enseignant introuvable.");

        if (!asPlatformAdmin)
            EnsureExpertCanManageTeacher(tenantId, actorUserId);

        var now = DateTime.UtcNow;
        tenant.Status = TenantStatus.Active;
        tenant.IsPublicProfile = true;
        tenant.OnboardingCompletedAt ??= now;
        if (tenant.LicenseExpiresAt is null || tenant.LicenseExpiresAt <= now)
            tenant.LicenseExpiresAt = now.AddYears(1);

        if (tenant.ExpertApprovalStatus != ExpertApprovalStatus.Approved)
        {
            tenant.ExpertApprovalStatus = ExpertApprovalStatus.Approved;
            tenant.ExpertApprovedAt ??= now;
            tenant.ApprovedByUserId ??= actorUserId;
            tenant.ExpertApprovalNotes ??= asPlatformAdmin
                ? "Fiche publique publiée par un administrateur plateforme."
                : "Fiche publique publiée par un expert du groupe.";
        }

        if (!asPlatformAdmin && tenant.ApprovedByExpertGroupId is null)
        {
            var groupId = db.ExpertGroupMembers
                .Where(m => m.UserId == actorUserId && m.Status == ExpertMembershipStatus.Active)
                .Select(m => m.ExpertGroupId)
                .FirstOrDefault();
            if (groupId != Guid.Empty)
                tenant.ApprovedByExpertGroupId = groupId;
        }

        tenant.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return new PublishTeacherPublicProfileResult(
            tenant.Id,
            tenant.Slug,
            tenant.IsPublicProfile,
            $"/school/{tenant.Slug}");
    }

    public async Task<PublishTeacherPublicProfileResult> UnpublishPublicProfileAsync(
        Guid tenantId,
        string actorUserId,
        bool asPlatformAdmin,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École / enseignant introuvable.");

        if (!asPlatformAdmin)
            EnsureExpertCanManageTeacher(tenantId, actorUserId);

        tenant.IsPublicProfile = false;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new PublishTeacherPublicProfileResult(
            tenant.Id,
            tenant.Slug,
            tenant.IsPublicProfile,
            $"/school/{tenant.Slug}");
    }

    public void EnsureExpertCanManageTeacher(Guid tenantId, string expertUserId)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException("École / enseignant introuvable.");

        var groupIds = db.ExpertGroupMembers
            .Where(m => m.UserId == expertUserId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();

        if (groupIds.Count == 0)
            throw new InvalidOperationException("Vous n'êtes membre d'aucun groupe d'experts.");

        if (tenant.ApprovedByExpertGroupId is Guid gid && groupIds.Contains(gid))
            return;

        // Dossier encore en file pour le pays du groupe de l'expert
        if (tenant.ExpertApprovalStatus is ExpertApprovalStatus.Pending
            or ExpertApprovalStatus.Assigned
            or ExpertApprovalStatus.UnderReview
            or ExpertApprovalStatus.ChangesRequested)
        {
            var countries = db.ExpertGroups
                .Where(g => groupIds.Contains(g.Id) && g.IsActive)
                .Select(g => g.CountryCode)
                .ToList();
            var home = ProfileVisibility.NormalizeCode(tenant.Country);
            if (countries.Any(c => string.IsNullOrWhiteSpace(c)
                                   || string.Equals(ProfileVisibility.NormalizeCode(c), home, StringComparison.OrdinalIgnoreCase)))
                return;
        }

        throw new InvalidOperationException(
            "Vous n'êtes pas autorisé à gérer cet enseignant (hors périmètre de votre groupe).");
    }

    private async Task<TeacherSchoolRecordDto> MapAsync(Tenant tenant, CancellationToken ct)
    {
        string email = "";
        string first = "";
        string last = "";
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(tenant.OwnerUserId))
        {
            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is not null)
            {
                email = contact.Value.Email ?? "";
                // DisplayName often "First Last"
                var parts = (contact.Value.DisplayName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1) first = parts[0];
                if (parts.Length >= 2) last = parts[1];
            }
        }

        string? groupName = null;
        if (tenant.ApprovedByExpertGroupId is Guid gid)
            groupName = db.ExpertGroups.Where(g => g.Id == gid).Select(g => g.Name).FirstOrDefault();

        var visible = ProfileVisibility.Parse(tenant.VisibleCountryCodes);
        if (visible.Count == 0)
        {
            var home = ProfileVisibility.NormalizeCode(tenant.Country);
            if (home.Length == 2) visible = [home];
        }

        var branding = db.TenantBrandings.FirstOrDefault(b => b.TenantId == tenant.Id);

        return new TeacherSchoolRecordDto(
            tenant.Id,
            tenant.OwnerUserId,
            email,
            first,
            last,
            phone,
            tenant.Name,
            tenant.Slug,
            tenant.Description,
            branding?.Presentation,
            tenant.City,
            tenant.Country,
            tenant.Language,
            tenant.Currency,
            visible,
            tenant.IsPublicProfile,
            tenant.Status == TenantStatus.Active,
            tenant.HasValidLicense(),
            tenant.OnboardingCompletedAt is not null,
            tenant.LicenseExpiresAt,
            (int)tenant.ExpertApprovalStatus,
            tenant.ApprovedByExpertGroupId,
            groupName);
    }
}
