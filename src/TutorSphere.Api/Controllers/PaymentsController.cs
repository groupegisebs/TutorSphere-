using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly IEmailService _email;
    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PaymentsController(
        IPaymentGatewayService paymentGateway,
        IEmailService email,
        IApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _paymentGateway = paymentGateway;
        _email = email;
        _db = db;
        _userManager = userManager;
    }

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
            var response = await _paymentGateway.CreateSubscriptionCheckoutAsync(subscriptionId, request, ct);

            // Look up the student subscription to notify the parent
            var subscription = _db.StudentSubscriptions.FirstOrDefault(s => s.Id == subscriptionId);
            if (subscription is not null)
            {
                var student = _db.Students.FirstOrDefault(s => s.Id == subscription.StudentId);
                if (student is not null)
                {
                    var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == student.ParentProfileId);
                    if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
                    {
                        await _email.SendParentPaymentReceiptAsync(
                            parent.Email,
                            parent.FirstName,
                            $"{student.FirstName} {student.LastName}".Trim(),
                            response.Amount,
                            response.CheckoutUrl,
                            ct);
                    }
                }
            }

            return Ok(response);
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

    /// <summary>Après retour Stripe Checkout : sync Pay Gateway → active l'abonnement (retries).</summary>
    [HttpPost("subscriptions/{subscriptionId:guid}/confirm")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<PaymentStatusResponse>> ConfirmSubscriptionPayment(
        Guid subscriptionId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _paymentGateway.ConfirmSubscriptionPaymentAsync(subscriptionId, ct: ct));
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
            var response = await _paymentGateway.CancelSubscriptionAsync(subscriptionId, cancelImmediately, ct);

            var currentUserId = User.GetUserId();
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var currentUser = await _userManager.FindByIdAsync(currentUserId);
                if (currentUser is not null && !string.IsNullOrWhiteSpace(currentUser.Email))
                {
                    var roles = await _userManager.GetRolesAsync(currentUser);
                    if (roles.Contains(UserRoles.Tutor))
                        await _email.SendTutorSubscriptionCancelledAsync(currentUser.Email, currentUser.FirstName, ct);
                    else
                        await _email.SendParentPaymentFailedAsync(currentUser.Email, currentUser.FirstName, ct);
                }
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
