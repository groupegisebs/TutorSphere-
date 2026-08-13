using Microsoft.AspNetCore.Http;
using TutorSphere.Application.Services;

namespace TutorSphere.Api;

public static class GroupAdminActAs
{
    public static Guid? ReadGroupId(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(IGroupAdminAccessService.ActAsHeaderName, out var raw))
            return null;
        return Guid.TryParse(raw.ToString(), out var id) ? id : null;
    }
}
