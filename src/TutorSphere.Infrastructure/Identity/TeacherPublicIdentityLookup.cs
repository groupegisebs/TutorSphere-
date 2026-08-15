using Microsoft.AspNetCore.Identity;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.Identity;

public sealed class TeacherPublicIdentityLookup : ITeacherPublicIdentityLookup
{
    private readonly UserManager<ApplicationUser> _users;

    public TeacherPublicIdentityLookup(UserManager<ApplicationUser> users) => _users = users;

    public Task<IReadOnlyDictionary<string, TeacherPublicNameParts>> GetByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return Task.FromResult<IReadOnlyDictionary<string, TeacherPublicNameParts>>(
                new Dictionary<string, TeacherPublicNameParts>());

        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var map = _users.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToList()
            .ToDictionary(
                u => u.Id,
                u => new TeacherPublicNameParts(u.FirstName ?? "", u.LastName ?? ""),
                StringComparer.Ordinal);

        return Task.FromResult<IReadOnlyDictionary<string, TeacherPublicNameParts>>(map);
    }
}
