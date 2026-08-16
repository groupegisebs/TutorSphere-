using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api.Filters;
using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/tutor/plan-subjects")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
[RequireActiveTutorLicense]
public class TutorPlanSubjectsController(ITutorPlanCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TutorPlanSubjectDto>>> List(CancellationToken ct)
    {
        try
        {
            return Ok(await catalog.ListSubjectsAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
