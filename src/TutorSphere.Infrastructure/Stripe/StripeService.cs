using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Infrastructure.Stripe;

public class StripeService : IStripeService
{
    private const decimal MinCommissionPercent = 5m;
    private const decimal MaxCommissionPercent = 15m;

    private readonly IApplicationDbContext _db;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IApplicationDbContext db,
        IOptions<StripeSettings> settings,
        ILogger<StripeService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public StripeConfigDto GetConfig() => new(_settings.PublishableKey);

    public async Task<ConnectOnboardingResponse> CreateConnectOnboardingAsync(
        Guid tenantId,
        ConnectOnboardingRequest request,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        if (string.IsNullOrEmpty(tenant.StripeAccountId))
        {
            var accountService = new AccountService();
            var account = await accountService.CreateAsync(new AccountCreateOptions
            {
                Type = "express",
                Country = NormalizeCountry(tenant.Country),
                Email = null,
                Capabilities = new AccountCapabilitiesOptions
                {
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                    Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenant.Id.ToString(),
                    ["tenant_slug"] = tenant.Slug
                }
            }, cancellationToken: ct);

            tenant.StripeAccountId = account.Id;
            await _db.SaveChangesAsync(ct);
        }

        var linkService = new AccountLinkService();
        var accountLink = await linkService.CreateAsync(new AccountLinkCreateOptions
        {
            Account = tenant.StripeAccountId,
            RefreshUrl = request.RefreshUrl,
            ReturnUrl = request.ReturnUrl,
            Type = "account_onboarding"
        }, cancellationToken: ct);

        return new ConnectOnboardingResponse(tenant.StripeAccountId, accountLink.Url);
    }

    public async Task<ConnectAccountStatusResponse> GetConnectAccountStatusAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        if (string.IsNullOrEmpty(tenant.StripeAccountId))
            throw new InvalidOperationException("Aucun compte Stripe Connect associé.");

        var account = await new AccountService().GetAsync(tenant.StripeAccountId, cancellationToken: ct);
        return new ConnectAccountStatusResponse(
            account.Id,
            account.ChargesEnabled,
            account.DetailsSubmitted,
            account.PayoutsEnabled);
    }

    public async Task<ParentCustomerResponse> CreateOrGetParentCustomerAsync(
        Guid parentProfileId,
        CancellationToken ct = default)
    {
        var parent = await _db.ParentProfiles.FirstOrDefaultAsync(p => p.Id == parentProfileId, ct)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        if (!string.IsNullOrEmpty(parent.StripeCustomerId))
            return new ParentCustomerResponse(parent.Id, parent.StripeCustomerId);

        var customerService = new CustomerService();
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = parent.Email,
            Name = $"{parent.FirstName} {parent.LastName}".Trim(),
            Metadata = new Dictionary<string, string>
            {
                ["parent_profile_id"] = parent.Id.ToString(),
                ["tenant_id"] = parent.TenantId.ToString()
            }
        }, cancellationToken: ct);

        parent.StripeCustomerId = customer.Id;
        await _db.SaveChangesAsync(ct);

        return new ParentCustomerResponse(parent.Id, customer.Id);
    }

    public async Task<SubscriptionPaymentIntentResponse> CreateSubscriptionPaymentIntentAsync(
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        var subscription = await _db.StudentSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        var offering = await _db.SubscriptionOfferings
            .FirstOrDefaultAsync(o => o.Id == subscription.OfferingId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        var student = await _db.Students
            .FirstOrDefaultAsync(s => s.Id == subscription.StudentId, ct)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        if (string.IsNullOrEmpty(tenant.StripeAccountId))
            throw new InvalidOperationException("Le tuteur doit compléter l'onboarding Stripe Connect.");

        var parent = await CreateOrGetParentCustomerAsync(student.ParentProfileId, ct);
        var commissionPercent = ClampCommission(tenant.PlatformCommissionPercent);
        var amount = offering.Price;
        var platformFee = Math.Round(amount * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var tutorAmount = amount - platformFee;
        var amountCents = ToStripeAmount(amount);
        var platformFeeCents = ToStripeAmount(platformFee);

        var payment = new Payment
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            Amount = amount,
            PlatformFee = platformFee,
            TutorAmount = tutorAmount,
            Currency = offering.Currency,
            Status = PaymentStatus.Pending
        };
        _db.Add(payment);
        await _db.SaveChangesAsync(ct);

        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = offering.Currency.ToLowerInvariant(),
            Customer = parent.StripeCustomerId,
            ApplicationFeeAmount = platformFeeCents,
            TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = tenant.StripeAccountId
            },
            Metadata = new Dictionary<string, string>
            {
                ["payment_id"] = payment.Id.ToString(),
                ["subscription_id"] = subscription.Id.ToString(),
                ["tenant_id"] = tenant.Id.ToString(),
                ["commission_percent"] = commissionPercent.ToString("0.##")
            },
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            }
        }, cancellationToken: ct);

        payment.StripePaymentIntentId = paymentIntent.Id;
        await _db.SaveChangesAsync(ct);

        return new SubscriptionPaymentIntentResponse(
            payment.Id,
            paymentIntent.ClientSecret,
            paymentIntent.Id,
            amount,
            platformFee,
            tutorAmount,
            offering.Currency);
    }

    public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
    {
        var stripeEvent = EventUtility.ConstructEvent(json, signature, _settings.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                await HandlePaymentIntentSucceededAsync(stripeEvent, ct);
                break;
            case "payment_intent.payment_failed":
                await HandlePaymentIntentFailedAsync(stripeEvent, ct);
                break;
            case "account.updated":
                await HandleAccountUpdatedAsync(stripeEvent, ct);
                break;
            default:
                _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandlePaymentIntentSucceededAsync(Event stripeEvent, CancellationToken ct)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null)
            return;

        var payment = await FindPaymentAsync(paymentIntent, ct);
        if (payment is null)
        {
            _logger.LogWarning("Payment not found for PaymentIntent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;

        if (payment.InvoiceId.HasValue)
        {
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId, ct);
            if (invoice is not null)
            {
                invoice.Status = PaymentStatus.Completed;
                invoice.PaidAt = DateTime.UtcNow;
            }
        }

        if (payment.SubscriptionId.HasValue)
        {
            var subscription = await _db.StudentSubscriptions
                .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId, ct);
            if (subscription is not null)
                subscription.Status = SubscriptionStatus.Active;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandlePaymentIntentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent is null)
            return;

        var payment = await FindPaymentAsync(paymentIntent, ct);
        if (payment is null)
            return;

        payment.Status = PaymentStatus.Failed;
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleAccountUpdatedAsync(Event stripeEvent, CancellationToken ct)
    {
        var account = stripeEvent.Data.Object as Account;
        if (account is null)
            return;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.StripeAccountId == account.Id, ct);
        if (tenant is null)
            return;

        if (account.ChargesEnabled && tenant.Status == TenantStatus.PendingValidation)
            tenant.Status = TenantStatus.Active;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Payment?> FindPaymentAsync(PaymentIntent paymentIntent, CancellationToken ct)
    {
        if (paymentIntent.Metadata.TryGetValue("payment_id", out var paymentIdValue)
            && Guid.TryParse(paymentIdValue, out var paymentId))
        {
            return await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        }

        return await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id, ct);
    }

    private static decimal ClampCommission(decimal percent) =>
        Math.Clamp(percent, MinCommissionPercent, MaxCommissionPercent);

    private static long ToStripeAmount(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static string NormalizeCountry(string? country) =>
        string.IsNullOrWhiteSpace(country) ? "CA" : country.Trim().ToUpperInvariant();
}
