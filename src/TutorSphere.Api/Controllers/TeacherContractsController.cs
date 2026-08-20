using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Api;
using TutorSphere.Application.DTOs.Contracts;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api")]
public class TeacherContractsController(
    ITeacherContractService contracts,
    IGroupAdminAccessService groupAccess) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Guid? ActAsGroupId => GroupAdminActAs.ReadGroupId(Request);
    private bool AsPlatformActAs => groupAccess.IsPlatformAdmin(User) && ActAsGroupId.HasValue;
    private ContractClientContext Client => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());

    [HttpGet("expert/contracts/template")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public ActionResult<TeacherContractTemplateDto> ExpertTemplate() => Ok(contracts.GetTemplate());

    [HttpGet("expert/contracts/teachers")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<TeacherContractTeacherOptionDto>>> ExpertTeachers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            return Ok(await contracts.ListTeacherOptionsAsync(UserId, AsPlatformActAs, ActAsGroupId, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("expert/contracts")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<TeacherContractListItemDto>>> ExpertList(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.ListForGroupAsync(UserId, AsPlatformActAs, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("expert/contracts")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractListItemDto>> ExpertSend(
        [FromBody] SendTeacherContractRequest request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.CreateAndSendAsync(UserId, request, AsPlatformActAs, ActAsGroupId, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("expert/contracts/{id:guid}")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractDetailDto>> ExpertGet(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.GetAsync(id, UserId, AsPlatformActAs, ActAsGroupId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("expert/contracts/{id:guid}/cancel")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<IActionResult> ExpertCancel(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await contracts.CancelAsync(id, UserId, AsPlatformActAs, ActAsGroupId, Client, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("expert/contracts/{id:guid}/resend")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractListItemDto>> ExpertResend(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.ResendAsync(id, UserId, AsPlatformActAs, ActAsGroupId, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("expert/contracts/{id:guid}/pdf")]
    [Authorize(Roles = $"{UserRoles.GroupManager},{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public Task<IActionResult> ExpertPdf(Guid id, CancellationToken ct) => PdfAsync(id, AsPlatformActAs, ActAsGroupId, ct);

    [HttpGet("admin/contracts/template")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public ActionResult<TeacherContractTemplateDto> AdminTemplate() => Ok(contracts.GetTemplate());

    [HttpGet("admin/contracts/teachers")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<TeacherContractTeacherOptionDto>>> AdminTeachers(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await contracts.ListTeacherOptionsAsync(UserId, true, null, ct));
    }

    [HttpGet("admin/contracts")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<IReadOnlyList<TeacherContractListItemDto>>> AdminList(CancellationToken ct)
        => Ok(await contracts.ListAllForPlatformAdminAsync(ct));

    [HttpPost("admin/contracts")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractListItemDto>> AdminSend(
        [FromBody] SendTeacherContractRequest request, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.CreateAndSendAsync(UserId, request, true, ActAsGroupId, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("admin/contracts/{id:guid}")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractDetailDto>> AdminGet(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.GetAsync(id, UserId, true, null, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("admin/contracts/{id:guid}/cancel")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<IActionResult> AdminCancel(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            await contracts.CancelAsync(id, UserId, true, null, Client, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("admin/contracts/{id:guid}/resend")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public async Task<ActionResult<TeacherContractListItemDto>> AdminResend(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.ResendAsync(id, UserId, true, ActAsGroupId, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("admin/contracts/{id:guid}/pdf")]
    [Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
    public Task<IActionResult> AdminPdf(Guid id, CancellationToken ct) => PdfAsync(id, true, null, ct);

    [HttpGet("tutor/contracts")]
    [Authorize(Roles = UserRoles.Tutor)]
    public async Task<ActionResult<IReadOnlyList<TeacherContractListItemDto>>> TutorList(CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        return Ok(await contracts.ListForTeacherAsync(UserId, ct));
    }

    [HttpGet("tutor/contracts/{id:guid}")]
    [Authorize(Roles = UserRoles.Tutor)]
    public async Task<ActionResult<TeacherContractDetailDto>> TutorGet(Guid id, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try { return Ok(await contracts.GetAsync(id, UserId, false, null, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("tutor/contracts/{id:guid}/pdf")]
    [Authorize(Roles = UserRoles.Tutor)]
    public Task<IActionResult> TutorPdf(Guid id, CancellationToken ct) => PdfAsync(id, false, null, ct);

    [AllowAnonymous]
    [HttpGet("contracts/sign/{token}")]
    public async Task<ActionResult<TeacherContractSignViewDto>> SignGet(string token, CancellationToken ct)
    {
        try { return Ok(await contracts.GetByTokenAsync(token, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("contracts/sign/{token}/sections/{sectionKey}/open")]
    public async Task<IActionResult> SignOpen(string token, string sectionKey, CancellationToken ct)
    {
        try
        {
            await contracts.OpenSectionAsync(token, sectionKey, Client, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("contracts/sign/{token}/sections/{sectionKey}/decide")]
    public async Task<IActionResult> SignDecide(
        string token, string sectionKey, [FromBody] DecideContractSectionRequest request, CancellationToken ct)
    {
        try
        {
            await contracts.DecideSectionAsync(token, sectionKey, request, Client, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("contracts/sign/{token}/refuse")]
    public async Task<IActionResult> SignRefuse(
        string token, [FromBody] RefuseContractRequest request, CancellationToken ct)
    {
        try
        {
            await contracts.RefuseAsync(token, request, Client, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("contracts/sign/{token}/complete")]
    public async Task<ActionResult<TeacherContractDetailDto>> SignComplete(
        string token, [FromBody] CompleteContractSignatureRequest request, CancellationToken ct)
    {
        try { return Ok(await contracts.CompleteSignatureAsync(token, request, Client, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("contracts/verify/{contractNumber}")]
    public async Task<ActionResult<TeacherContractVerifyDto>> Verify(string contractNumber, CancellationToken ct)
    {
        var dto = await contracts.VerifyAsync(contractNumber, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    private async Task<IActionResult> PdfAsync(Guid id, bool asPlatform, Guid? actAs, CancellationToken ct)
    {
        if (UserId is null) return Unauthorized();
        try
        {
            var file = await contracts.GetPdfIfAllowedAsync(id, UserId, asPlatform, actAs, Client, ct);
            if (file is null) return NotFound(new { error = "Document signé introuvable." });
            return File(file.Value.Bytes, "application/pdf", file.Value.FileName);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
