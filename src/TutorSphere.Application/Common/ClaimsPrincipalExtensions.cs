using System.Security.Claims;

namespace TutorSphere.Application.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Resolves the user id from JWT claims whether inbound mapping is enabled or not.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value;
}
