using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public BrandingController(IBrandingService brandingService, UserManager<ApplicationUser> userManager)
    {
        _brandingService = brandingService;
        _userManager = userManager;
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTenantSiteDto>> GetPublicSite(string slug, CancellationToken ct)
    {
        var site = await _brandingService.GetPublicSiteBySlugAsync(slug, ct);
        return site is null ? NotFound() : Ok(site);
    }

    /// <summary>Full public tutor/school profile for directory "View profile".</summary>
    [HttpGet("{slug}/tutor")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTutorDetailDto>> GetPublicTutorDetail(string slug, CancellationToken ct)
    {
        var detail = await _brandingService.GetPublicTutorDetailAsync(slug, ct);
        if (detail is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(detail.OwnerUserId))
            return Ok(detail with { TutorFullName = detail.SchoolName });

        var owner = await _userManager.FindByIdAsync(detail.OwnerUserId);
        if (owner is null)
            return Ok(detail with { TutorFullName = detail.SchoolName });

        var fullName = owner.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = detail.SchoolName;

        return Ok(detail with
        {
            TutorFirstName = owner.FirstName,
            TutorLastName = owner.LastName,
            TutorFullName = fullName,
            Language = string.IsNullOrWhiteSpace(owner.PreferredLanguage)
                ? detail.Language
                : owner.PreferredLanguage
        });
    }

    [HttpGet("tenant/{tenantId:guid}")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<TenantBrandingDto>> GetBranding(Guid tenantId, CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        var branding = await _brandingService.GetBrandingAsync(tenantId, ct);
        if (branding is not null)
            return Ok(branding);

        // Empty branding so the UI can load before the first save.
        return Ok(new TenantBrandingDto(
            Guid.Empty, tenantId, null, null, "#2563eb", "#1e40af", null, null));
    }

    [HttpPut("tenant/{tenantId:guid}")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
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
