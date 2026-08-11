using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert")]
[Authorize(Roles = UserRoles.Expert)]
public class ExpertApprovalsController : ControllerBase
{
    private readonly IExpertApprovalService _approvals;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExpertApprovalsController(IExpertApprovalService approvals, UserManager<ApplicationUser> userManager)
    {
        _approvals = approvals;
        _userManager = userManager;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<PendingTeacherDto>>> Queue(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        var list = await _approvals.ListPendingForExpertAsync(UserId, ct);
        var enriched = new List<PendingTeacherDto>();
        foreach (var item in list)
        {
            string? email = null;
            string? name = null;
            if (!string.IsNullOrWhiteSpace(item.OwnerEmail) || item.TenantId != Guid.Empty)
            {
                // Owner contact filled below if we can resolve from review detail owner id later.
            }

            var detail = await _approvals.GetReviewDetailAsync(item.TenantId, ct);
            if (detail?.OwnerUserId is { } oid)
            {
                var user = await _userManager.FindByIdAsync(oid);
                email = user?.Email;
                name = user?.FullName;
            }

            enriched.Add(item with { OwnerEmail = email, OwnerName = name });
        }

        return Ok(enriched);
    }

    [HttpGet("teachers/{tenantId:guid}")]
    public async Task<ActionResult<TeacherReviewDetailDto>> GetTeacher(Guid tenantId, CancellationToken ct)
    {
        var detail = await _approvals.GetReviewDetailAsync(tenantId, ct);
        if (detail is null) return NotFound(new { error = "Fiche introuvable." });

        if (!string.IsNullOrWhiteSpace(detail.OwnerUserId))
        {
            var user = await _userManager.FindByIdAsync(detail.OwnerUserId);
            detail = detail with
            {
                OwnerEmail = user?.Email,
                OwnerName = user?.FullName
            };
        }

        return Ok(detail);
    }

    [HttpPost("teachers/{tenantId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid tenantId, [FromBody] ExpertDecisionRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.ApproveAsync(tenantId, UserId, request?.Notes, ct);
            return Ok(new { message = "Enseignant approuvé." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("teachers/{tenantId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid tenantId, [FromBody] ExpertDecisionRequest? request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await _approvals.RejectAsync(tenantId, UserId, request?.Notes, ct);
            return Ok(new { message = "Enseignant rejeté." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
