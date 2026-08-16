using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api;
using TutorSphere.Application.DTOs.TutorPayouts;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/expert/teacher-payouts")]
[Authorize(Roles = $"{UserRoles.Expert},{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class ExpertTeacherPayoutsController(
    IGroupTeacherPayoutService payouts,
    IExpertApprovalService approvals,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupTeacherPayoutInvoiceDto>>> List(
        [FromQuery] string? tab, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await payouts.ListForGroupAsync(await RequireGroupIdAsync(ct), tab, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/processing")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<GroupTeacherPayoutInvoiceDto>> MarkProcessing(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await payouts.MarkProcessingAsync(await RequireGroupIdAsync(ct), id, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/paid")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<GroupTeacherPayoutInvoiceDto>> MarkPaid(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await payouts.MarkPaidAsync(await RequireGroupIdAsync(ct), id, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var (content, fileName) = await payouts.BuildPdfAsync(id, await RequireGroupIdAsync(ct), tenantId: null, ct);
            return File(content, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Guid> RequireGroupIdAsync(CancellationToken ct)
    {
        var managed = await groupAccess.ResolveManagedGroupAsync(User, ActAsGroupId, ct);
        if (managed is not null)
            return managed.Id;
        if (groupAccess.IsPlatformAdmin(User) && ActAsGroupId is Guid gid)
            return gid;
        if (UserId is null)
            throw new InvalidOperationException("Utilisateur introuvable.");
        var mine = await approvals.GetMyGroupAsync(UserId, ct)
            ?? throw new InvalidOperationException("Accès réservé au responsable du groupe.");
        return mine.Id;
    }
}
