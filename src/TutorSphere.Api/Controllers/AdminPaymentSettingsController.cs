using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/admin/payment-settings")]
[Authorize(Roles = $"{UserRoles.SuperAdmin},{UserRoles.PlatformAdmin}")]
public class AdminPaymentSettingsController(IPlatformPaymentSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformPaymentSettingsDto>> Get(CancellationToken ct)
        => Ok(await settings.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<PlatformPaymentSettingsDto>> Update(
        [FromBody] UpdatePlatformPaymentSettingsRequest request,
        CancellationToken ct)
        => Ok(await settings.UpdateAsync(request, ct));
}
