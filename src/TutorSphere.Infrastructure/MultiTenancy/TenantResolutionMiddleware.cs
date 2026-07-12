using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Infrastructure.MultiTenancy;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ApplicationDbContext db)
    {
        var slug = ResolveTenantSlug(context);
        if (!string.IsNullOrEmpty(slug))
        {
            var tenant = await db.TenantsSet
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug || t.Subdomain == slug);

            if (tenant is not null)
            {
                if (TenantClaimConflicts(context, tenant.Id))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Le locataire ne correspond pas au jeton d'authentification." });
                    return;
                }

                tenantContext.SetTenant(tenant.Id, tenant.Slug);
                await _next(context);
                return;
            }
        }

        // Blazor / API clients authenticate with JWT but do not send X-Tenant-Slug.
        var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
            tenantContext.SetTenant(tenantId);

        await _next(context);
    }

    private static bool TenantClaimConflicts(HttpContext context, Guid resolvedTenantId)
    {
        var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdClaim, out var claimTenantId) && claimTenantId != resolvedTenantId;
    }

    private static string? ResolveTenantSlug(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString().ToLowerInvariant();

        if (context.Request.Query.TryGetValue("tenant", out var query) && !string.IsNullOrWhiteSpace(query))
            return query.ToString().ToLowerInvariant();

        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3 && parts[^2] == "tutorsphere")
            return parts[0].ToLowerInvariant();

        return null;
    }
}
