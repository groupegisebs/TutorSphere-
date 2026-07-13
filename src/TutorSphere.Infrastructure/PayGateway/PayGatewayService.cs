using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Infrastructure.PayGateway;

internal sealed class PayGatewayService : IPaymentGatewayService
{
    private const decimal MinCommissionPercent = 5m;
    private const decimal MaxCommissionPercent = 15m;

    private readonly IApplicationDbContext _db;
    private readonly PayGatewayClient _gateway;
    private readonly PayGatewaySettings _settings;
    private readonly ISubscriptionLessonScheduler _lessonScheduler;
    private readonly IInvoiceService _invoices;
    private readonly ILogger<PayGatewayService> _logger;
    private string? _cachedPublishableKey;

    public PayGatewayService(
        IApplicationDbContext db,
        PayGatewayClient gateway,
        IOptions<PayGatewaySettings> settings,
        ISubscriptionLessonScheduler lessonScheduler,
        IInvoiceService invoices,
        ILogger<PayGatewayService> logger)
    {
        _db = db;
        _gateway = gateway;
        _settings = settings.Value;
        _lessonScheduler = lessonScheduler;
        _invoices = invoices;
        _logger = logger;
    }

    public PaymentGatewayConfigDto GetConfig() => new(_cachedPublishableKey);

    public async Task<ParentCustomerResponse> CreateOrGetParentCustomerAsync(
        Guid parentProfileId,
        CancellationToken ct = default)
    {
        var parent = await _db.ParentProfilesForAnyTenant.FirstOrDefaultAsync(p => p.Id == parentProfileId, ct)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        // Code stable (Live). En sandbox, préfixe SBX- pour éviter de réutiliser un cus_… Live
        // stocké côté Pay Gateway (objets Stripe séparés entre test et live).
        var stableCode = parent.StripeCustomerId is { Length: > 0 } existing
            && !existing.StartsWith("SBX-", StringComparison.OrdinalIgnoreCase)
            ? existing
            : parent.Id.ToString("N").ToUpperInvariant();

        if (string.IsNullOrEmpty(parent.StripeCustomerId)
            || parent.StripeCustomerId.StartsWith("SBX-", StringComparison.OrdinalIgnoreCase))
        {
            parent.StripeCustomerId = stableCode;
            await _db.SaveChangesAsync(ct);
        }

        var customerCode = _gateway.UsesSandbox
            ? TruncateCustomerCode($"SBX-{stableCode}")
            : stableCode;

        return new ParentCustomerResponse(parent.Id, customerCode);
    }

    private static string TruncateCustomerCode(string code) =>
        code.Length <= 50 ? code : code[..50];

    public async Task<SubscriptionCheckoutResponse> CreateSubscriptionCheckoutAsync(
        Guid subscriptionId,
        CreateSubscriptionCheckoutRequest request,
        CancellationToken ct = default)
    {
        var subscription = await _db.StudentSubscriptionsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        if (subscription.Status != SubscriptionStatus.AwaitingPayment)
            throw new InvalidOperationException(
                "Le paiement n'est possible qu'après acceptation de la demande par l'enseignant.");

        var offering = await _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefaultAsync(o => o.Id == subscription.OfferingId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        var student = await _db.StudentsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscription.StudentId, ct)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        var parent = await _db.ParentProfilesForAnyTenant.FirstOrDefaultAsync(p => p.Id == student.ParentProfileId, ct)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var customer = await CreateOrGetParentCustomerAsync(parent.Id, ct);
        var commissionPercent = ClampCommission(tenant.PlatformCommissionPercent);
        var amount = offering.Price;
        var platformFee = Math.Round(amount * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var tutorAmount = amount - platformFee;

        var productCode = ToProductCode(offering.Id);
        var planCode = ResolvePlanCode(offering.DurationDays);
        await EnsureCatalogItemAsync(offering, productCode, planCode, ct);

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

        var metadata = JsonSerializer.Serialize(new
        {
            payment_id = payment.Id,
            subscription_id = subscription.Id,
            tenant_id = tenant.Id,
            commission_percent = commissionPercent.ToString("0.##")
        });

        var checkout = await _gateway.CreateCheckoutSessionAsync(new GatewayCheckoutSessionRequest(
            customer.CustomerCode,
            parent.Email,
            $"{parent.FirstName} {parent.LastName}".Trim(),
            parent.UserId,
            productCode,
            planCode,
            request.SuccessUrl,
            request.CancelUrl,
            metadata,
            TrialDays: null), ct);

        payment.StripePaymentIntentId = checkout.PaymentCode;
        _cachedPublishableKey ??= checkout.PublishableKey;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Checkout PayGateway créé pour l'abonnement {SubscriptionId} (paymentCode={PaymentCode}, stripeMode={StripeMode})",
            subscription.Id,
            checkout.PaymentCode,
            checkout.StripeMode ?? "?");

        return new SubscriptionCheckoutResponse(
            payment.Id,
            checkout.PaymentCode,
            checkout.CheckoutUrl,
            checkout.SessionId,
            checkout.ClientSecret,
            amount,
            platformFee,
            tutorAmount,
            offering.Currency);
    }

    public async Task<PaymentStatusResponse> SyncPaymentStatusAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _db.PaymentsForAnyTenant.FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException("Paiement introuvable.");

        if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
            throw new InvalidOperationException("Aucun code de paiement passerelle associé.");

        var gatewayPayment = await _gateway.GetPaymentAsync(payment.StripePaymentIntentId, ct)
            ?? throw new InvalidOperationException("Paiement introuvable dans la passerelle.");

        await ApplyGatewayPaymentStatusAsync(payment, gatewayPayment, ct);

        return new PaymentStatusResponse(
            payment.Id,
            gatewayPayment.PaymentCode,
            gatewayPayment.Status,
            payment.Status.ToString(),
            payment.CompletedAt);
    }

    public async Task<PaymentStatusResponse> ConfirmSubscriptionPaymentAsync(
        Guid subscriptionId,
        int maxAttempts = 5,
        int retryDelayMs = 2000,
        CancellationToken ct = default)
    {
        var payment = await _db.PaymentsForAnyTenant
            .Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Aucun paiement trouvé pour cet abonnement.");

        if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
            throw new InvalidOperationException("Aucun code de paiement passerelle associé.");

        maxAttempts = Math.Max(1, maxAttempts);
        PaymentStatusResponse? last = null;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                last = await SyncPaymentStatusAsync(payment.Id, ct);
                if (string.Equals(last.GatewayStatus, "Succeeded", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(last.LocalStatus, nameof(PaymentStatus.Completed), StringComparison.OrdinalIgnoreCase))
                {
                    return last;
                }
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                _logger.LogInformation(
                    ex,
                    "Confirm paiement abonnement {SubscriptionId} tentative {Attempt}/{Max} — en attente webhook",
                    subscriptionId,
                    attempt,
                    maxAttempts);
            }

            if (attempt < maxAttempts)
                await Task.Delay(retryDelayMs, ct);
        }

        if (last is not null)
            return last;

        throw lastError
            ?? new InvalidOperationException("Paiement encore en attente de confirmation côté passerelle.");
    }

    public async Task<IReadOnlyList<GatewaySubscriptionResponse>> GetParentSubscriptionsAsync(
        Guid parentProfileId,
        CancellationToken ct = default)
    {
        var customer = await CreateOrGetParentCustomerAsync(parentProfileId, ct);
        var subscriptions = await _gateway.GetCustomerSubscriptionsAsync(customer.CustomerCode, ct);

        return subscriptions
            .Select(s => new GatewaySubscriptionResponse(
                s.SubscriptionCode,
                s.Status,
                s.ProductCode,
                s.PlanCode,
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.CancelAtPeriodEnd))
            .ToList();
    }

    public async Task<CancelSubscriptionResponse> CancelSubscriptionAsync(
        Guid subscriptionId,
        bool cancelImmediately,
        CancellationToken ct = default)
    {
        var subscription = await _db.StudentSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new InvalidOperationException("Aucun abonnement passerelle associé.");

        var result = await _gateway.CancelSubscriptionAsync(
            new GatewayCancelSubscriptionRequest(subscription.StripeSubscriptionId, cancelImmediately),
            ct);

        subscription.Status = MapSubscriptionStatus(result.Status);
        await _db.SaveChangesAsync(ct);

        return new CancelSubscriptionResponse(
            result.SubscriptionCode,
            result.Status,
            result.CancelledAt);
    }

    public async Task SyncOfferingCatalogAsync(Guid offeringId, CancellationToken ct = default)
    {
        var offering = await _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefaultAsync(o => o.Id == offeringId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        var productCode = ToProductCode(offering.Id);
        var planCode = ResolvePlanCode(offering.DurationDays);
        await EnsureCatalogItemAsync(offering, productCode, planCode, ct);
    }

    private async Task EnsureCatalogItemAsync(
        SubscriptionOffering offering,
        string productCode,
        string planCode,
        CancellationToken ct)
    {
        await _gateway.CreateCatalogItemAsync(new GatewayCreateCatalogItemRequest(
            productCode,
            offering.Title,
            offering.Description,
            planCode,
            offering.Title,
            offering.Price,
            offering.Currency.ToLowerInvariant(),
            ResolveBillingInterval(offering.DurationDays),
            SyncToStripe: true), ct);

        _logger.LogInformation(
            "Offre {OfferingId} synchronisée vers Pay Gateway/Stripe ({ProductCode}/{PlanCode}, {Amount} {Currency})",
            offering.Id,
            productCode,
            planCode,
            offering.Price,
            offering.Currency);
    }

    private async Task ApplyGatewayPaymentStatusAsync(
        Payment payment,
        GatewayPaymentResponse gatewayPayment,
        CancellationToken ct)
    {
        var mapped = MapPaymentStatus(gatewayPayment.Status);
        if (mapped == payment.Status && mapped != PaymentStatus.Completed)
            return;

        payment.Status = mapped;

        if (mapped == PaymentStatus.Completed)
        {
            payment.CompletedAt = gatewayPayment.PaidAt ?? DateTime.UtcNow;

            if (payment.InvoiceId.HasValue)
            {
                var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId, ct);
                if (invoice is not null)
                {
                    invoice.Status = PaymentStatus.Completed;
                    invoice.PaidAt = payment.CompletedAt;
                }
            }

            if (payment.SubscriptionId.HasValue)
            {
                var subscription = await _db.StudentSubscriptions
                    .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId, ct);
                if (subscription is null)
                {
                    subscription = await _db.StudentSubscriptionsForAnyTenant
                        .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId, ct);
                }

                if (subscription is not null)
                {
                    if (subscription.Status is SubscriptionStatus.Rejected or SubscriptionStatus.Cancelled)
                    {
                        _logger.LogWarning(
                            "Paiement {PaymentId} ignoré pour abonnement {SubscriptionId} (statut {Status})",
                            payment.Id,
                            subscription.Id,
                            subscription.Status);
                    }
                    else
                    {
                        subscription.Status = SubscriptionStatus.Active;
                        await TryLinkGatewaySubscriptionAsync(subscription, gatewayPayment.CustomerCode, ct);
                        await _db.SaveChangesAsync(ct);
                        try
                        {
                            await _invoices.EnsureInvoiceForPaymentAsync(payment.Id, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Facture non créée pour le paiement {PaymentId}", payment.Id);
                        }

                        await _lessonScheduler.EnsureScheduledAsync(subscription.Id, ct);
                        return;
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        if (mapped == PaymentStatus.Completed && !payment.InvoiceId.HasValue)
        {
            try
            {
                await _invoices.EnsureInvoiceForPaymentAsync(payment.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Facture non créée pour le paiement {PaymentId}", payment.Id);
            }
        }
    }

    private async Task TryLinkGatewaySubscriptionAsync(
        StudentSubscription subscription,
        string customerCode,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            return;

        var productCode = ToProductCode(subscription.OfferingId);
        var gatewaySubscriptions = await _gateway.GetCustomerSubscriptionsAsync(customerCode, ct);
        var match = gatewaySubscriptions
            .Where(s => s.ProductCode.Equals(productCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.CurrentPeriodStart)
            .FirstOrDefault();

        if (match is not null)
            subscription.StripeSubscriptionId = match.SubscriptionCode;
    }

    private static string ToProductCode(Guid offeringId) =>
        $"OFF-{offeringId:N}".ToUpperInvariant();

    private static string ResolvePlanCode(int durationDays) => durationDays switch
    {
        <= 0 => "ONE-TIME",
        <= 31 => "MONTHLY",
        _ => "YEARLY"
    };

    private static string ResolveBillingInterval(int durationDays) => durationDays switch
    {
        <= 0 => "OneTime",
        <= 31 => "Monthly",
        _ => "Yearly"
    };

    private static decimal ClampCommission(decimal percent) =>
        Math.Clamp(percent, MinCommissionPercent, MaxCommissionPercent);

    private static PaymentStatus MapPaymentStatus(string gatewayStatus) =>
        gatewayStatus.ToUpperInvariant() switch
        {
            "SUCCEEDED" => PaymentStatus.Completed,
            "FAILED" or "CANCELLED" => PaymentStatus.Failed,
            "REFUNDED" => PaymentStatus.Refunded,
            _ => PaymentStatus.Pending
        };

    private static SubscriptionStatus MapSubscriptionStatus(string gatewayStatus) =>
        gatewayStatus.ToUpperInvariant() switch
        {
            "ACTIVE" => SubscriptionStatus.Active,
            "PASTDUE" => SubscriptionStatus.Paused,
            "CANCELLED" or "CANCELED" => SubscriptionStatus.Cancelled,
            "EXPIRED" => SubscriptionStatus.Expired,
            _ => SubscriptionStatus.Pending
        };
}
