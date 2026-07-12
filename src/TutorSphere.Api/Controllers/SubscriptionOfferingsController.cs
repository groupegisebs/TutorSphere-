using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.StudentSubscriptions;
using TutorSphere.Application.DTOs.SubscriptionOfferings;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/subscription-offerings")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class SubscriptionOfferingsController : ControllerBase
{
    private readonly ISubscriptionOfferingService _offeringService;
    private readonly IStudentSubscriptionService _subscriptions;

    public SubscriptionOfferingsController(
        ISubscriptionOfferingService offeringService,
        IStudentSubscriptionService subscriptions)
    {
        _offeringService = offeringService;
        _subscriptions = subscriptions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubscriptionOfferingDto>>> List(CancellationToken ct)
        => Ok(await _offeringService.GetAllAsync(ct));

    /// <summary>Abonnés aux offres du tuteur (locataire courant).</summary>
    [HttpGet("subscribers")]
    public async Task<ActionResult<IReadOnlyList<StudentSubscriptionDto>>> Subscribers(CancellationToken ct)
        => Ok(await _subscriptions.GetForCurrentTenantAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriptionOfferingDto>> GetById(Guid id, CancellationToken ct)
    {
        var offering = await _offeringService.GetByIdAsync(id, ct);
        return offering is null ? NotFound() : Ok(offering);
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionOfferingDto>> Create(
        [FromBody] CreateSubscriptionOfferingRequest request,
        CancellationToken ct)
    {
        try
        {
            var offering = await _offeringService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = offering.Id }, offering);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SubscriptionOfferingDto>> Update(
        Guid id,
        [FromBody] UpdateSubscriptionOfferingRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _offeringService.UpdateAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _offeringService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<SubscriptionOfferingDto>> Activate(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _offeringService.ActivateAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<SubscriptionOfferingDto>> Deactivate(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _offeringService.DeactivateAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
