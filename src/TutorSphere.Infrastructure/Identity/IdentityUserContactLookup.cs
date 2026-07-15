using Microsoft.AspNetCore.Identity;
using TutorSphere.Application.Common.Interfaces;

namespace TutorSphere.Infrastructure.Identity;

public sealed class IdentityUserContactLookup : IUserContactLookup
{
    private readonly UserManager<ApplicationUser> _users;

    public IdentityUserContactLookup(UserManager<ApplicationUser> users) => _users = users;

    public async Task<(string Email, string DisplayName)?> GetAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user = await _users.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return null;

        var name = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.Email
            : $"{user.FirstName} {user.LastName}".Trim();
        return (user.Email, name);
    }
}
