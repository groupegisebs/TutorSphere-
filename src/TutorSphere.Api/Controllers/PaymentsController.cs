using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly IEmailService _email;
    private readonly IBillingEmailOrchestrator _billingEmail;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentGatewayService paymentGateway,
        IEmailService email,
        IBillingEmailOrchestrator billingEmail,
        UserManager<ApplicationUser> userManager,
        ILogger<PaymentsController> logger)
    {
        _paymentGateway = paymentGateway;
        _email = email;
        _billingEmail = billingEmail;
        _userManager = userManager;
        _logger = logger;
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
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<SubscriptionCheckoutResponse>> CreateSubscriptionCheckout(
        Guid subscriptionId,
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken ct)
    {
        try
        {
            var gate = await EnsureCanPayAsync(subscriptionId, ct);
            if (gate is not null)
                return gate;

            var response = await _paymentGateway.CreateSubscriptionCheckoutAsync(subscriptionId, request, ct);

            // Lien de paiement (INVOICE_READY) — le reçu part uniquement après succès.
            var payLink = response.CheckoutUrl
                ?? $"{Request.Scheme}://{Request.Host}/parent/subscriptions?sub={subscriptionId}";
            await _billingEmail.NotifyPaymentLinkReadyAsync(
                subscriptionId,
                payLink,
                response.Amount,
                ct);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            // Le message d'EF ne dit rien : seule l'exception interne porte l'erreur SQL réelle.
            _logger.LogError(
                ex,
                "Échec enregistrement du paiement pour l'abonnement {SubscriptionId} : {DbError}",
                subscriptionId,
                InnermostMessage(ex));
            return BadRequest(new
            {
                error = "Le paiement n'a pas pu être enregistré. L'équipe technique a été notifiée."
            });
        }
    }

    private static string InnermostMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current.Message;
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/payment-intent")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    [Obsolete("Utiliser POST /api/payments/subscriptions/{id}/checkout")]
    public Task<ActionResult<SubscriptionCheckoutResponse>> CreateSubscriptionPaymentIntent(
        Guid subscriptionId,
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken ct) =>
        CreateSubscriptionCheckout(subscriptionId, request, ct);

    [HttpGet("{paymentId:guid}/status")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
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
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<PaymentStatusResponse>> ConfirmSubscriptionPayment(
        Guid subscriptionId,
        CancellationToken ct)
    {
        try
        {
            var gate = await EnsureCanPayAsync(subscriptionId, ct);
            if (gate is not null)
                return gate;

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
            var gate = await EnsureCanPayAsync(subscriptionId, ct);
            if (gate is not null)
                return gate;

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

    /// <summary>Initie un paiement Orange Money WebPay / MTN MoMo Collections (Cameroun, XAF).</summary>
    [HttpPost("mobile-money")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<MobileMoneyChargeResponse>> CreateMobileMoneyCharge(
        [FromBody] CreateMobileMoneyChargeRequest request,
        CancellationToken ct)
    {
        try
        {
            var gate = await EnsureCanPayAsync(request.SubscriptionId, ct);
            if (gate is not null)
                return gate;

            var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var key)
                ? key.ToString()
                : request.IdempotencyKey;

            var response = await _paymentGateway.CreateMobileMoneyChargeAsync(
                request with { IdempotencyKey = idempotencyKey },
                ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Statut normalisé d'un paiement Mobile Money (poll serveur).</summary>
    [HttpGet("mobile-money/{paymentId:guid}/status")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<PaymentStatusResponse>> GetMobileMoneyStatus(
        Guid paymentId,
        CancellationToken ct)
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

    /// <summary>Devis taxe Afrique (HT → TTC) pour le pays du payeur.</summary>
    [HttpGet("tax/africa/quote")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<AfricanTaxQuoteDto>> QuoteAfricanTax(
        [FromQuery] decimal amountExclusive,
        [FromQuery] string currency = "XAF",
        [FromQuery] string countryCode = "CM",
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _paymentGateway.QuoteAfricanTaxAsync(amountExclusive, currency, countryCode, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Liste complète des taux TVA/GST des pays d'Afrique.</summary>
    [HttpGet("tax/africa/rates")]
    [Authorize(Roles = $"{UserRoles.Parent},{UserRoles.Student},{UserRoles.Tutor},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyList<AfricanTaxRateDto>>> ListAfricanTaxRates(CancellationToken ct) =>
        Ok(await _paymentGateway.ListAfricanTaxRatesAsync(ct));

    private async Task<ActionResult?> EnsureCanPayAsync(Guid subscriptionId, CancellationToken ct)
    {
        if (User.IsInRole(UserRoles.SuperAdmin) || User.IsInRole(UserRoles.PlatformAdmin))
            return null;

        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _paymentGateway.AssertUserCanPaySubscriptionAsync(userId, subscriptionId, ct);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
