using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IStripeService _stripeService;

    public PaymentsController(IStripeService stripeService) => _stripeService = stripeService;

    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<StripeConfigDto> GetConfig() => Ok(_stripeService.GetConfig());

    [HttpPost("connect/{tenantId:guid}/onboard")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<ConnectOnboardingResponse>> ConnectOnboard(
        Guid tenantId,
        [FromBody] ConnectOnboardingRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _stripeService.CreateConnectOnboardingAsync(tenantId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("connect/{tenantId:guid}/status")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<ConnectAccountStatusResponse>> ConnectStatus(Guid tenantId, CancellationToken ct)
    {
        try
        {
            return Ok(await _stripeService.GetConnectAccountStatusAsync(tenantId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("customers/parents/{parentProfileId:guid}")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Parent},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<ParentCustomerResponse>> CreateParentCustomer(
        Guid parentProfileId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _stripeService.CreateOrGetParentCustomerAsync(parentProfileId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/payment-intent")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<SubscriptionPaymentIntentResponse>> CreateSubscriptionPaymentIntent(
        Guid subscriptionId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _stripeService.CreateSubscriptionPaymentIntentAsync(subscriptionId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
