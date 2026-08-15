using Microsoft.AspNetCore.Identity;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public sealed class SupportInboxResolver(UserManager<ApplicationUser> users) : ISupportInboxResolver
{
    public async Task<string?> ResolveUserIdAsync(CancellationToken ct = default)
    {
        var supers = await users.GetUsersInRoleAsync(UserRoles.SuperAdmin);
        var first = supers.FirstOrDefault();
        if (first is not null)
            return first.Id;

        var admins = await users.GetUsersInRoleAsync(UserRoles.PlatformAdmin);
        return admins.FirstOrDefault()?.Id;
    }
}
