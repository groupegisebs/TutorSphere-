using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Tenants;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService) => _tenantService = tenantService;

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.CreateTenantAsync(request, ct);
            return CreatedAtAction(nameof(GetBySlug), new { slug = tenant.Slug }, tenant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var tenant = await _tenantService.GetBySlugAsync(slug, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpGet("check-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> CheckSlug(string slug, CancellationToken ct)
    {
        var existing = await _tenantService.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        return Ok(new { available = existing is null });
    }

    [HttpGet("{id:guid}/dashboard")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<TenantDashboardDto>> GetDashboard(Guid id, CancellationToken ct)
    {
        return Ok(await _tenantService.GetDashboardAsync(id, ct));
    }

    [HttpGet("{id:guid}/profile")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<TutorProfileDto>> GetProfile(Guid id, CancellationToken ct)
    {
        var profile = await _tenantService.GetProfileAsync(id, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("{id:guid}/profile")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<TutorProfileDto>> UpdateProfile(Guid id, [FromBody] UpdateTutorProfileRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _tenantService.UpdateProfileAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
