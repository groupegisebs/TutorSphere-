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
    private readonly IPaymentGatewayService _paymentGateway;

    public PaymentsController(IPaymentGatewayService paymentGateway) => _paymentGateway = paymentGateway;

    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<PaymentGatewayConfigDto> GetConfig() => Ok(_paymentGateway.GetConfig());

    [HttpPost("customers/parents/{parentProfileId:guid}")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Parent},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<ParentCustomerResponse>> CreateParentCustomer(
        Guid parentProfileId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _paymentGateway.CreateOrGetParentCustomerAsync(parentProfileId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/checkout")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<SubscriptionCheckoutResponse>> CreateSubscriptionCheckout(
        Guid subscriptionId,
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _paymentGateway.CreateSubscriptionCheckoutAsync(subscriptionId, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/payment-intent")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    [Obsolete("Utiliser POST /api/payments/subscriptions/{id}/checkout")]
    public Task<ActionResult<SubscriptionCheckoutResponse>> CreateSubscriptionPaymentIntent(
        Guid subscriptionId,
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken ct) =>
        CreateSubscriptionCheckout(subscriptionId, request, ct);

    [HttpGet("{paymentId:guid}/status")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<PaymentStatusResponse>> SyncPaymentStatus(Guid paymentId, CancellationToken ct)
    {
        try
        {
            return Ok(await _paymentGateway.SyncPaymentStatusAsync(paymentId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("customers/parents/{parentProfileId:guid}/subscriptions")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyList<GatewaySubscriptionResponse>>> GetParentSubscriptions(
        Guid parentProfileId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _paymentGateway.GetParentSubscriptionsAsync(parentProfileId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/cancel")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<CancelSubscriptionResponse>> CancelSubscription(
        Guid subscriptionId,
        [FromQuery] bool cancelImmediately = false,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _paymentGateway.CancelSubscriptionAsync(subscriptionId, cancelImmediately, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
