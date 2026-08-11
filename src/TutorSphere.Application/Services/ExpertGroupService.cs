using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IExpertGroupService
{
    Task<IReadOnlyList<ExpertGroupDto>> ListAsync(CancellationToken ct = default);
    Task<ExpertGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExpertGroupDto> CreateAsync(CreateExpertGroupRequest request, CancellationToken ct = default);
    Task<ExpertGroupDto> UpdateAsync(Guid id, UpdateExpertGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExpertGroupMemberDto>> ListMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<ExpertGroupMemberDto> AddMemberAsync(Guid groupId, string userId, CancellationToken ct = default);
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
            .GroupBy(m => m.ExpertGroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        IReadOnlyList<ExpertGroupDto> result = groups
            .Select(g => Map(g, memberCounts.GetValueOrDefault(g.Id)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<ExpertGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var g = db.ExpertGroups.FirstOrDefault(x => x.Id == id);
        if (g is null) return Task.FromResult<ExpertGroupDto?>(null);
        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id);
        return Task.FromResult<ExpertGroupDto?>(Map(g, count));
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

        var entity = new ExpertGroup
        {
            Name = name,
            LogoUrl = TrimOrNull(request.LogoUrl),
            ContactEmail = TrimOrNull(request.ContactEmail),
            ContactPhone = TrimOrNull(request.ContactPhone),
            CountryCode = country,
            IsInternational = isInternational,
            IsActive = true
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);
        return Map(entity, 0);
    }

    public async Task<ExpertGroupDto> UpdateAsync(Guid id, UpdateExpertGroupRequest request, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom du groupe est requis.");

        entity.Name = name;
        entity.ContactEmail = TrimOrNull(request.ContactEmail);
        entity.ContactPhone = TrimOrNull(request.ContactPhone);
        entity.LogoUrl = TrimOrNull(request.LogoUrl);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var count = db.ExpertGroupMembers.Count(m => m.ExpertGroupId == id);
        return Map(entity, count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = db.ExpertGroups.FirstOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable.");

        var members = db.ExpertGroupMembers.Where(m => m.ExpertGroupId == id).ToList();
        foreach (var m in members)
            db.Remove(m);

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ExpertGroupMemberDto>> ListMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!db.ExpertGroups.Any(g => g.Id == groupId))
            throw new InvalidOperationException("Groupe d'experts introuvable.");

        // Email/name résolus côté API via UserManager.
        IReadOnlyList<ExpertGroupMemberDto> result = db.ExpertGroupMembers
            .Where(m => m.ExpertGroupId == groupId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ExpertGroupMemberDto(m.Id, m.ExpertGroupId, m.UserId, string.Empty, string.Empty))
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<ExpertGroupMemberDto> AddMemberAsync(Guid groupId, string userId, CancellationToken ct = default)
    {
        if (!db.ExpertGroups.Any(g => g.Id == groupId))
            throw new InvalidOperationException("Groupe d'experts introuvable.");
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Utilisateur requis.");

        if (db.ExpertGroupMembers.Any(m => m.ExpertGroupId == groupId && m.UserId == userId))
            throw new InvalidOperationException("Cet utilisateur est déjà membre du groupe.");

        var member = new ExpertGroupMember
        {
            ExpertGroupId = groupId,
            UserId = userId.Trim()
        };
        db.Add(member);
        await db.SaveChangesAsync(ct);
        return new ExpertGroupMemberDto(member.Id, groupId, member.UserId, string.Empty, string.Empty);
    }

    public async Task RemoveMemberAsync(Guid groupId, string userId, CancellationToken ct = default)
    {
        var member = db.ExpertGroupMembers.FirstOrDefault(m => m.ExpertGroupId == groupId && m.UserId == userId)
            ?? throw new InvalidOperationException("Membre introuvable.");
        db.Remove(member);
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

    private static ExpertGroupDto Map(ExpertGroup g, int memberCount) =>
        new(g.Id, g.Name, g.LogoUrl, g.ContactEmail, g.ContactPhone,
            g.CountryCode, g.IsInternational, g.IsActive, memberCount, g.CreatedAt);

    private static string? NormalizeCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return code.Trim().ToUpperInvariant();
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
