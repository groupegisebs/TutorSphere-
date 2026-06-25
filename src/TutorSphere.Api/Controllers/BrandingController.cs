using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;

    public BrandingController(IBrandingService brandingService) => _brandingService = brandingService;

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTenantSiteDto>> GetPublicSite(string slug, CancellationToken ct)
    {
        var site = await _brandingService.GetPublicSiteBySlugAsync(slug, ct);
        return site is null ? NotFound() : Ok(site);
    }

    [HttpGet("tenant/{tenantId:guid}")]
    [Authorize(Roles = UserRoles.Tutor)]
    public async Task<ActionResult<TenantBrandingDto>> GetBranding(Guid tenantId, CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        var branding = await _brandingService.GetBrandingAsync(tenantId, ct);
        return branding is null ? NotFound() : Ok(branding);
    }

    [HttpPut("tenant/{tenantId:guid}")]
    [Authorize(Roles = UserRoles.Tutor)]
    public async Task<ActionResult<TenantBrandingDto>> UpdateBranding(
        Guid tenantId,
        [FromBody] UpdateTenantBrandingRequest request,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        try
        {
            return Ok(await _brandingService.UpdateBrandingAsync(tenantId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private bool CanAccessTenant(Guid tenantId)
    {
        if (User.IsInRole(UserRoles.SuperAdmin))
            return true;

        var claim = User.FindFirst("tenant_id")?.Value;
        return claim is not null
            && Guid.TryParse(claim, out var userTenantId)
            && userTenantId == tenantId;
    }
}
