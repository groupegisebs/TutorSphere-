using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.Filters;

/// <summary>
/// Bloque les routes métier tuteur si la licence annuelle plateforme n'est pas valide.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireActiveTutorLicenseAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Admins bypass
        if (user.IsInRole(UserRoles.SuperAdmin) || user.IsInRole(UserRoles.PlatformAdmin))
        {
            await next();
            return;
        }

        if (!user.IsInRole(UserRoles.Tutor) && !user.IsInRole(UserRoles.TeachingAssistant))
        {
            await next();
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.TenantsSet.AsNoTracking()
            .FirstOrDefaultAsync(t => t.OwnerUserId == userId);

        // Assistant : résoudre via User.TenantId
        if (tenant is null)
        {
            var appUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (appUser?.TenantId is Guid tid)
                tenant = await db.TenantsSet.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid);
        }

        if (tenant is null || !tenant.HasValidLicense())
        {
            var code = tenant is not null && tenant.RequiresOnboarding()
                ? "ONBOARDING_REQUIRED"
                : "PLATFORM_LICENSE_REQUIRED";
            var url = code == "ONBOARDING_REQUIRED" ? "/tutor/onboarding" : "/tutor/activate";
            var message = code == "ONBOARDING_REQUIRED"
                ? "Auto-formation obligatoire avant d'utiliser le système."
                : "Licence annuelle requise pour activer la session enseignant.";

            context.Result = new ObjectResult(new
            {
                error = message,
                code,
                activateUrl = url
            })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            return;
        }

        await next();
    }
}
