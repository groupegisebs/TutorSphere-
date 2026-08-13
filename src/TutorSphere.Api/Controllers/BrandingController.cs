using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common;
using TutorSphere.Application.DTOs.Branding;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;
    private readonly IParentService _parentService;
    private readonly IStudentPortalService _studentPortal;
    private readonly UserManager<ApplicationUser> _userManager;

    public BrandingController(
        IBrandingService brandingService,
        IParentService parentService,
        IStudentPortalService studentPortal,
        UserManager<ApplicationUser> userManager)
    {
        _brandingService = brandingService;
        _parentService = parentService;
        _studentPortal = studentPortal;
        _userManager = userManager;
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTenantSiteDto>> GetPublicSite(
        string slug,
        [FromQuery] string? viewerCountry,
        CancellationToken ct)
    {
        var country = await ResolveViewerCountryAsync(viewerCountry, ct);
        if (RequiresCountryFilter() && country is null)
            return NotFound();

        var site = await _brandingService.GetPublicSiteBySlugAsync(slug, country, ct);
        return site is null ? NotFound() : Ok(site);
    }

    /// <summary>Full public tutor/school profile for directory "View profile".</summary>
    [HttpGet("{slug}/tutor")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicTutorDetailDto>> GetPublicTutorDetail(
        string slug,
        [FromQuery] string? viewerCountry,
        CancellationToken ct)
    {
        var country = await ResolveViewerCountryAsync(viewerCountry, ct);
        // Parent / élève : pays obligatoire (même règle que la recherche).
        if (RequiresCountryFilter() && country is null)
            return NotFound();

        var detail = await _brandingService.GetPublicTutorDetailAsync(slug, country, ct);
        if (detail is null)
            return NotFound();

        ApplicationUser? owner = null;
        if (!string.IsNullOrWhiteSpace(detail.OwnerUserId))
            owner = await _userManager.FindByIdAsync(detail.OwnerUserId);

        owner ??= _userManager.Users.FirstOrDefault(u => u.TenantId == detail.TenantId);

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

    private bool RequiresCountryFilter() =>
        User.IsInRole(UserRoles.Parent) || User.IsInRole(UserRoles.Student);

    private async Task<string?> ResolveViewerCountryAsync(string? requested, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            if (User.IsInRole(UserRoles.Parent))
            {
                var parent = await _parentService.GetByUserIdAsync(userId, ct);
                var fromProfile = ProfileVisibility.NormalizeCode(parent?.Country);
                if (fromProfile.Length == 2)
                    return fromProfile;
            }
            else if (User.IsInRole(UserRoles.Student))
            {
                var fromProfile = ProfileVisibility.NormalizeCode(
                    await _studentPortal.GetViewerCountryAsync(userId, ct));
                if (fromProfile.Length == 2)
                    return fromProfile;
            }
        }

        var fromQuery = ProfileVisibility.NormalizeCode(requested);
        return fromQuery.Length == 2 ? fromQuery : null;
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
