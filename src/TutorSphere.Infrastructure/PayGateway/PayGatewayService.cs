using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Payments;
using TutorSphere.Application.DTOs.PlatformBilling;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Infrastructure.PayGateway;

internal sealed class PayGatewayService : IPaymentGatewayService
{
    private readonly IApplicationDbContext _db;
    private readonly PayGatewayClient _gateway;
    private readonly PayGatewaySettings _settings;
    private readonly PlatformBillingOptions _platformBilling;
    private readonly ISubscriptionLessonScheduler _lessonScheduler;
    private readonly IInvoiceService _invoices;
    private readonly IBillingEmailOrchestrator _billingEmail;
    private readonly IUserContactLookup _contacts;
    private readonly IEmailService _email;
    private readonly IAppUrlProvider _urls;
    private readonly IPlatformPaymentSettingsService _paymentSettings;
    private readonly ILogger<PayGatewayService> _logger;
    private string? _cachedPublishableKey;

    public PayGatewayService(
        IApplicationDbContext db,
        PayGatewayClient gateway,
        IOptions<PayGatewaySettings> settings,
        IOptions<PlatformBillingOptions> platformBilling,
        ISubscriptionLessonScheduler lessonScheduler,
        IInvoiceService invoices,
        IBillingEmailOrchestrator billingEmail,
        IUserContactLookup contacts,
        IEmailService email,
        IAppUrlProvider urls,
        IPlatformPaymentSettingsService paymentSettings,
        ILogger<PayGatewayService> logger)
    {
        _db = db;
        _gateway = gateway;
        _settings = settings.Value;
        _platformBilling = platformBilling.Value;
        _lessonScheduler = lessonScheduler;
        _invoices = invoices;
        _billingEmail = billingEmail;
        _contacts = contacts;
        _email = email;
        _urls = urls;
        _paymentSettings = paymentSettings;
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

        var offering = await _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefaultAsync(o => o.Id == subscription.OfferingId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        EnsureSubscriptionPayable(subscription, offering);

        var student = await _db.StudentsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscription.StudentId, ct)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        var parent = await _db.ParentProfilesForAnyTenant.FirstOrDefaultAsync(p => p.Id == student.ParentProfileId, ct)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var customer = await CreateOrGetParentCustomerAsync(parent.Id, ct);
        var paymentMethod = PaymentMethodCodes.Normalize(request.PaymentMethod);
        if (PaymentMethodCodes.IsDisabledCollectionChannel(paymentMethod))
            throw new InvalidOperationException(PaymentMethodCodes.MobileMoneyCollectionDisabledMessage);
        if (PaymentMethodCodes.IsMobileMoney(paymentMethod))
            throw new InvalidOperationException(
                "Le paiement Mobile Money s'initie via l'encaissement opérateur, pas via Stripe Checkout.");

        var amount = offering.Price;
        var split = await SplitParentPaymentAsync(tenant, amount, paymentMethod, ct);

        var productCode = ToProductCode(offering.Id);
        var planCode = SubscriptionPackRules.ResolvePlanCode(offering.DurationDays);
        await EnsureCatalogItemAsync(offering, productCode, planCode, ct);

        var payment = new Payment
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            Amount = amount,
            ProcessorFee = split.ProcessorFee,
            PlatformFee = split.PlatformFee,
            TutorAmount = split.TutorAmount,
            GroupAmount = split.GroupAmount,
            ExpertGroupId = split.ExpertGroupId,
            CommissionPercent = split.CommissionPercent,
            Currency = offering.Currency,
            Status = PaymentStatus.Pending
        };
        _db.Add(payment);
        await _db.SaveChangesAsync(ct);

        var fullName = $"{parent.FirstName} {parent.LastName}".Trim();

        var metadata = JsonSerializer.Serialize(new
        {
            payment_id = payment.Id,
            subscription_id = subscription.Id,
            tenant_id = tenant.Id,
            commission_percent = split.CommissionPercent.ToString("0.##"),
            processor_fee = split.ProcessorFee.ToString("0.##"),
            payment_method = paymentMethod,
            expert_group_id = split.ExpertGroupId
        });

        IReadOnlyList<string> paymentMethodTypes = paymentMethod == PaymentMethodCodes.PayPal
            ? ["paypal"]
            : ["card"];

        var checkout = await _gateway.CreateCheckoutSessionAsync(new GatewayCheckoutSessionRequest(
            customer.CustomerCode,
            parent.Email,
            fullName,
            parent.UserId,
            productCode,
            planCode,
            request.SuccessUrl,
            request.CancelUrl,
            metadata,
            TrialDays: null,
            Embedded: false,
            PaymentMethodTypes: paymentMethodTypes), ct);

        payment.StripePaymentIntentId = checkout.PaymentCode;
        _cachedPublishableKey ??= checkout.PublishableKey;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Checkout PayGateway créé pour l'abonnement {SubscriptionId} (paymentCode={PaymentCode}, method={Method}, stripeMode={StripeMode})",
            subscription.Id,
            checkout.PaymentCode,
            paymentMethod,
            checkout.StripeMode ?? "?");

        return new SubscriptionCheckoutResponse(
            payment.Id,
            checkout.PaymentCode,
            checkout.CheckoutUrl,
            checkout.SessionId,
            checkout.ClientSecret,
            amount,
            split.PlatformFee,
            split.TutorAmount,
            offering.Currency,
            paymentMethod,
            split.ProcessorFee,
            split.GroupAmount);
    }

    public async Task<PaymentStatusResponse> SyncPaymentStatusAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _db.PaymentsForAnyTenant.FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException("Paiement introuvable.");

        if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
            throw new InvalidOperationException("Aucun code de paiement passerelle associé.");

        // Mobile Money : rafraîchir via endpoint dédié (poll fournisseur) avant lecture générique.
        if (!string.IsNullOrWhiteSpace(payment.Channel))
        {
            var mmStatus = await _gateway.GetMobileMoneyStatusAsync(payment.StripePaymentIntentId, ct);
            if (mmStatus is not null)
            {
                var mappedMm = new GatewayPaymentResponse(
                    mmStatus.PaymentCode,
                    mmStatus.Status,
                    mmStatus.Amount,
                    mmStatus.Currency,
                    "",
                    "",
                    "",
                    DateTime.UtcNow,
                    mmStatus.PaidAt,
                    mmStatus.FailureReason,
                    null,
                    null);
                await ApplyGatewayPaymentStatusAsync(payment, mappedMm, ct);
                return new PaymentStatusResponse(
                    payment.Id,
                    mmStatus.PaymentCode,
                    mmStatus.Status,
                    payment.Status.ToString(),
                    payment.CompletedAt);
            }
        }

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

    public async Task TryCancelGatewaySubscriptionAsync(
        Guid subscriptionId,
        bool cancelImmediately,
        CancellationToken ct = default)
    {
        var subscription = await _db.StudentSubscriptionsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return;

        if (!_gateway.IsConfigured)
        {
            _logger.LogWarning(
                "Pay Gateway non configuré — annulation locale uniquement pour l'abonnement {SubscriptionId}.",
                subscriptionId);
            return;
        }

        try
        {
            var result = await _gateway.CancelSubscriptionAsync(
                new GatewayCancelSubscriptionRequest(subscription.StripeSubscriptionId, cancelImmediately),
                ct);
            subscription.Status = MapSubscriptionStatus(result.Status);
            subscription.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Impossible d'annuler l'abonnement passerelle {Code} (local {SubscriptionId}).",
                subscription.StripeSubscriptionId,
                subscriptionId);
        }
    }

    public async Task RefundCompletedPaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _db.PaymentsForAnyTenant.FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException("Paiement introuvable.");

        if (payment.Status == PaymentStatus.Refunded)
            return;

        if (payment.Status != PaymentStatus.Completed)
            throw new InvalidOperationException($"Le paiement {paymentId} n'est pas encaissé (statut {payment.Status}).");

        var isMobileMoney = !string.IsNullOrWhiteSpace(payment.Channel);

        if (_gateway.IsConfigured && !string.IsNullOrWhiteSpace(payment.StripePaymentIntentId) && !isMobileMoney)
        {
            var result = await _gateway.RefundPaymentAsync(payment.StripePaymentIntentId, ct);
            payment.Status = MapPaymentStatus(result.Status);
            if (payment.Status != PaymentStatus.Refunded)
                payment.Status = PaymentStatus.Refunded;
        }
        else if (_gateway.IsConfigured && !string.IsNullOrWhiteSpace(payment.StripePaymentIntentId) && isMobileMoney)
        {
            try
            {
                var result = await _gateway.RefundPaymentAsync(payment.StripePaymentIntentId, ct);
                payment.Status = MapPaymentStatus(result.Status);
                if (payment.Status != PaymentStatus.Refunded)
                    payment.Status = PaymentStatus.Refunded;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Remboursement Mobile Money non exécuté par la passerelle — marquage local du paiement {PaymentId}.",
                    paymentId);
                payment.Status = PaymentStatus.Refunded;
            }
        }
        else
        {
            if (!_gateway.IsConfigured)
                _logger.LogWarning(
                    "Pay Gateway non configuré — remboursement local uniquement pour le paiement {PaymentId}.",
                    paymentId);
            payment.Status = PaymentStatus.Refunded;
        }

        payment.UpdatedAt = DateTime.UtcNow;
        if (payment.InvoiceId is Guid invoiceId)
        {
            var invoice = await _db.InvoicesForAnyTenant.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice is not null)
            {
                invoice.Status = PaymentStatus.Refunded;
                invoice.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncOfferingCatalogAsync(Guid offeringId, CancellationToken ct = default)
    {
        var offering = await _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefaultAsync(o => o.Id == offeringId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        var productCode = ToProductCode(offering.Id);
        var planCode = SubscriptionPackRules.ResolvePlanCode(offering.DurationDays);
        await EnsureCatalogItemAsync(offering, productCode, planCode, ct);
    }

    private async Task EnsureCatalogItemAsync(
        SubscriptionOffering offering,
        string productCode,
        string planCode,
        CancellationToken ct)
    {
        var isXaf = offering.Currency.Equals("XAF", StringComparison.OrdinalIgnoreCase);
        await _gateway.CreateCatalogItemAsync(new GatewayCreateCatalogItemRequest(
            productCode,
            offering.Title,
            offering.Description,
            planCode,
            offering.Title,
            offering.Price,
            offering.Currency.ToLowerInvariant(),
            SubscriptionPackRules.ResolveBillingInterval(offering.DurationDays),
            SyncToStripe: !isXaf), ct);

        _logger.LogInformation(
            "Offre {OfferingId} synchronisée vers Pay Gateway/Stripe ({ProductCode}/{PlanCode}, {Amount} {Currency})",
            offering.Id,
            productCode,
            planCode,
            offering.Price,
            offering.Currency);
    }

    public async Task AssertUserCanPaySubscriptionAsync(
        string userId,
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentification requise.");

        var subscription = await _db.StudentSubscriptionsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        var student = await _db.StudentsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscription.StudentId, ct)
            ?? throw new InvalidOperationException("Élève introuvable.");

        string? parentUserId = null;
        if (student.ParentProfileId is Guid parentId)
        {
            var parent = await _db.ParentProfilesForAnyTenant
                .FirstOrDefaultAsync(p => p.Id == parentId, ct);
            parentUserId = parent?.UserId;
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct);
        if (PackPaymentProcess.CanCallerPay(userId, student.UserId, parentUserId, tenant?.OwnerUserId))
            return;

        throw new UnauthorizedAccessException("Vous n'êtes pas autorisé à payer cet abonnement.");
    }

    public async Task<int> SyncPendingPaymentsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);
        var pending = await _db.PaymentsForAnyTenant
            .Where(p => p.Status == PaymentStatus.Pending
                        && p.StripePaymentIntentId != null
                        && p.StripePaymentIntentId != ""
                        && p.CreatedAt >= cutoff)
            .OrderBy(p => p.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var synced = 0;
        foreach (var payment in pending)
        {
            try
            {
                await SyncPaymentStatusAsync(payment.Id, ct);
                synced++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync paiement en attente {PaymentId} échouée", payment.Id);
            }
        }

        return synced;
    }

    public async Task SyncPaymentByGatewayCodeAsync(string paymentCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentCode))
            return;

        var code = paymentCode.Trim();
        var payment = await _db.PaymentsForAnyTenant
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == code, ct);
        if (payment is null)
        {
            _logger.LogInformation("Webhook paiement : code {PaymentCode} inconnu localement", code);
            return;
        }

        await SyncPaymentStatusAsync(payment.Id, ct);
    }

    private async Task ApplyGatewayPaymentStatusAsync(
        Payment payment,
        GatewayPaymentResponse gatewayPayment,
        CancellationToken ct)
    {
        var previousStatus = payment.Status;
        var mapped = PackPaymentProcess.MapGatewayStatus(gatewayPayment.Status);

        if (PackPaymentProcess.Decide(previousStatus, mapped, null) == PackPaymentProcess.Decision.AlreadyApplied)
            return;

        if (mapped == payment.Status && mapped != PaymentStatus.Completed)
            return;

        payment.Status = mapped;

        if (mapped == PaymentStatus.Failed && previousStatus != PaymentStatus.Failed)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            if (payment.SubscriptionId is Guid failedSubId)
                await _billingEmail.NotifyPaymentFailedAsync(failedSubId, ct);
            return;
        }

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
                    var gate = PackPaymentProcess.Decide(previousStatus, mapped, subscription.Status);
                    if (gate == PackPaymentProcess.Decision.RefundClosedSubscription)
                    {
                        _logger.LogWarning(
                            "Paiement {PaymentId} encaissé après {Status} — remboursement du forfait {SubscriptionId}",
                            payment.Id,
                            subscription.Status,
                            subscription.Id);
                        await _db.SaveChangesAsync(ct);
                        try
                        {
                            await RefundCompletedPaymentAsync(payment.Id, ct);
                            await _billingEmail.NotifyPaymentRefundedAsync(payment.Id, "TutorSphere", ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Remboursement auto échoué pour paiement {PaymentId}", payment.Id);
                        }

                        return;
                    }

                    var offering = await _db.SubscriptionOfferingsForAnyTenant
                        .FirstOrDefaultAsync(o => o.Id == subscription.OfferingId, ct);
                    var durationDays = offering?.DurationDays > 0 ? offering.DurationDays : 30;
                    var credits = offering is null ? 0 : Math.Max(0, offering.SessionCount);
                    PackPaymentProcess.ActivatePack(subscription, credits, durationDays, DateTime.UtcNow);

                    if (previousStatus != PaymentStatus.Completed)
                    {
                        var teacher = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == payment.TenantId, ct);
                        if (teacher is not null)
                            LicenseFeeWithholding.TakeFromTutorShare(teacher, payment);
                    }

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

                    if (previousStatus != PaymentStatus.Completed)
                        await _billingEmail.NotifyPaymentSucceededAsync(payment.Id, ct);
                    return;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        if (mapped == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
        {
            var teacher = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == payment.TenantId, ct);
            if (teacher is not null)
            {
                LicenseFeeWithholding.TakeFromTutorShare(teacher, payment);
                await _db.SaveChangesAsync(ct);
            }
        }

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

        if (mapped == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
            await _billingEmail.NotifyPaymentSucceededAsync(payment.Id, ct);
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

    private static void EnsureSubscriptionPayable(
        StudentSubscription subscription,
        SubscriptionOffering offering) =>
        PackPaymentProcess.EnsurePayable(subscription, offering.DurationDays, DateTime.UtcNow);

    private static string ToProductCode(Guid offeringId) =>
        $"OFF-{offeringId:N}".ToUpperInvariant();

    private async Task<ParentPaymentSplit> SplitParentPaymentAsync(
        Tenant tenant,
        decimal amount,
        string paymentMethod,
        CancellationToken ct)
    {
        var settings = await _paymentSettings.GetEntityAsync(ct);
        Guid? groupId = tenant.ApprovedByExpertGroupId;
        var commission = settings.DefaultCommissionPercent;
        if (groupId is Guid gid && gid != Guid.Empty)
        {
            var group = await _db.ExpertGroups.FirstOrDefaultAsync(g => g.Id == gid, ct);
            if (group is not null)
                commission = group.PlatformCommissionPercent;
            else
                groupId = null;
        }
        else
        {
            groupId = null;
        }

        var method = PaymentMethodCodes.Normalize(paymentMethod);
        var (feePercent, feeFixed) = method switch
        {
            PaymentMethodCodes.PayPal => (settings.PayPalFeePercent, settings.PayPalFeeFixed),
            PaymentMethodCodes.MtnMomo or PaymentMethodCodes.OrangeMoney =>
                (settings.MobileMoneyFeePercent, settings.MobileMoneyFeeFixed),
            _ => (settings.CardFeePercent, settings.CardFeeFixed)
        };
        return ParentPaymentSplitCalculator.Compute(
            amount,
            feePercent,
            feeFixed,
            commission,
            groupId);
    }

    private static PaymentStatus MapPaymentStatus(string gatewayStatus) =>
        PackPaymentProcess.MapGatewayStatus(gatewayStatus);

    private static SubscriptionStatus MapSubscriptionStatus(string gatewayStatus) =>
        gatewayStatus.ToUpperInvariant() switch
        {
            "ACTIVE" => SubscriptionStatus.Active,
            "PASTDUE" => SubscriptionStatus.Paused,
            "CANCELLED" or "CANCELED" => SubscriptionStatus.Cancelled,
            "EXPIRED" => SubscriptionStatus.Expired,
            _ => SubscriptionStatus.Pending
        };

    public async Task<PlatformLicenseCheckoutResponse> CreatePlatformLicenseCheckoutAsync(
        Guid tenantId,
        CreatePlatformLicenseCheckoutRequest request,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Profil enseignant introuvable.");

        var contact = await _contacts.GetAsync(tenant.OwnerUserId, ct)
            ?? throw new InvalidOperationException("Coordonnées du propriétaire introuvables.");

        var amount = _platformBilling.AnnualFeeCad;
        var currency = string.IsNullOrWhiteSpace(_platformBilling.Currency)
            ? "CAD"
            : _platformBilling.Currency.ToUpperInvariant();
        var productCode = string.IsNullOrWhiteSpace(_platformBilling.ProductCode)
            ? "TUTORSPHERE-LICENSE-ANNUAL"
            : _platformBilling.ProductCode.Trim().ToUpperInvariant();
        var planCode = string.IsNullOrWhiteSpace(_platformBilling.PlanCode)
            ? "YEARLY"
            : _platformBilling.PlanCode.Trim().ToUpperInvariant();

        await _gateway.CreateCatalogItemAsync(new GatewayCreateCatalogItemRequest(
            productCode,
            "Licence annuelle enseignant",
            "Activation / renouvellement de la session enseignant (10 $ USD / an)",
            planCode,
            "Annuel",
            amount,
            currency.ToLowerInvariant(),
            "Yearly",
            SyncToStripe: true), ct);

        var licensePayment = new PlatformLicensePayment
        {
            TenantId = tenant.Id,
            Amount = amount,
            Currency = currency,
            Status = PaymentStatus.Pending
        };
        _db.Add(licensePayment);
        await _db.SaveChangesAsync(ct);

        var customerCode = TruncateCustomerCode(
            _gateway.UsesSandbox
                ? $"SBX-TUT-{tenant.Id:N}"
                : $"TUT-{tenant.Id:N}".ToUpperInvariant());

        var metadata = JsonSerializer.Serialize(new
        {
            platform_license_payment_id = licensePayment.Id,
            tenant_id = tenant.Id,
            type = "platform_license_annual"
        });

        var successUrl = AppendQuery(request.SuccessUrl, "paymentId", licensePayment.Id.ToString("D"));
        var paymentMethod = PaymentMethodCodes.Normalize(request.PaymentMethod);

        IReadOnlyList<string> paymentMethodTypes = paymentMethod == PaymentMethodCodes.PayPal
            ? ["paypal"]
            : ["card"];

        var checkout = await _gateway.CreateCheckoutSessionAsync(new GatewayCheckoutSessionRequest(
            customerCode,
            contact.Email,
            contact.DisplayName,
            tenant.OwnerUserId,
            productCode,
            planCode,
            successUrl,
            request.CancelUrl,
            metadata,
            TrialDays: null,
            Embedded: false,
            PaymentMethodTypes: paymentMethodTypes), ct);

        licensePayment.GatewayPaymentCode = checkout.PaymentCode;
        _cachedPublishableKey ??= checkout.PublishableKey;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Checkout licence plateforme créé pour tenant {TenantId} (paymentCode={PaymentCode}, method={Method})",
            tenant.Id,
            checkout.PaymentCode,
            paymentMethod);

        return new PlatformLicenseCheckoutResponse(
            licensePayment.Id,
            checkout.PaymentCode,
            checkout.CheckoutUrl,
            checkout.SessionId,
            checkout.ClientSecret,
            amount,
            currency,
            paymentMethod);
    }

    public async Task<PaymentStatusResponse> ConfirmPlatformLicensePaymentAsync(
        Guid tenantId,
        Guid? paymentId = null,
        int maxAttempts = 5,
        int retryDelayMs = 2000,
        CancellationToken ct = default)
    {
        PlatformLicensePayment? payment;
        if (paymentId.HasValue)
        {
            payment = await _db.PlatformLicensePaymentsForAnyTenant
                .FirstOrDefaultAsync(p => p.Id == paymentId.Value && p.TenantId == tenantId, ct);
        }
        else
        {
            payment = await _db.PlatformLicensePaymentsForAnyTenant
                .Where(p => p.TenantId == tenantId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (payment is null)
            throw new InvalidOperationException("Aucun paiement de licence trouvé.");

        if (string.IsNullOrEmpty(payment.GatewayPaymentCode))
            throw new InvalidOperationException("Aucun code de paiement passerelle associé.");

        maxAttempts = Math.Max(1, maxAttempts);
        PaymentStatusResponse? last = null;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                last = await SyncPlatformLicensePaymentAsync(payment.Id, ct);
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
                    "Confirm licence tenant {TenantId} tentative {Attempt}/{Max}",
                    tenantId,
                    attempt,
                    maxAttempts);
            }

            if (attempt < maxAttempts)
                await Task.Delay(retryDelayMs, ct);
        }

        if (last is not null)
            return last;

        throw lastError
            ?? new InvalidOperationException("Le paiement de licence n'a pas encore été confirmé.");
    }

    private async Task<PaymentStatusResponse> SyncPlatformLicensePaymentAsync(
        Guid paymentId,
        CancellationToken ct)
    {
        var payment = await _db.PlatformLicensePaymentsForAnyTenant.FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException("Paiement de licence introuvable.");

        if (string.IsNullOrEmpty(payment.GatewayPaymentCode))
            throw new InvalidOperationException("Aucun code de paiement passerelle associé.");

        var gatewayPayment = await _gateway.GetPaymentAsync(payment.GatewayPaymentCode, ct)
            ?? throw new InvalidOperationException("Paiement introuvable dans la passerelle.");

        await ApplyPlatformLicensePaymentStatusAsync(payment, gatewayPayment, ct);

        return new PaymentStatusResponse(
            payment.Id,
            gatewayPayment.PaymentCode,
            gatewayPayment.Status,
            payment.Status.ToString(),
            payment.CompletedAt);
    }

    private async Task ApplyPlatformLicensePaymentStatusAsync(
        PlatformLicensePayment payment,
        GatewayPaymentResponse gatewayPayment,
        CancellationToken ct)
    {
        var previousStatus = payment.Status;
        var mapped = MapPaymentStatus(gatewayPayment.Status);
        if (mapped == payment.Status && mapped != PaymentStatus.Completed)
            return;

        payment.Status = mapped;

        if (mapped == PaymentStatus.Completed)
        {
            payment.CompletedAt = gatewayPayment.PaidAt ?? DateTime.UtcNow;

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == payment.TenantId, ct);
            if (tenant is not null)
            {
                var periodStart = DateTime.UtcNow;
                // Renouvellement : prolonger depuis l'échéance actuelle si encore future.
                if (tenant.LicenseExpiresAt is { } current && current > periodStart)
                    periodStart = current;

                var periodEnd = periodStart.AddYears(1);
                payment.PeriodStart = periodStart;
                payment.PeriodEnd = periodEnd;

                tenant.LicenseExpiresAt = periodEnd;
                tenant.LicenseRenewalReminderSentAt = null;
                tenant.LicenseFeeWithholdingRemainingUsd = 0;
                tenant.LicenseSettlementKind = LicenseFeeWithholding.SettlementPay;
                tenant.UpdatedAt = DateTime.UtcNow;

                // Première activation : formation obligatoire avant visibilité publique.
                // Renouvellement : garder Active si l'onboarding est déjà fait.
                if (tenant.OnboardingCompletedAt is null)
                {
                    tenant.Status = TenantStatus.AwaitingOnboarding;
                    tenant.IsPublicProfile = false;
                }
                else
                {
                    tenant.Status = TenantStatus.Active;
                    tenant.IsPublicProfile = true;
                }

                await _db.SaveChangesAsync(ct);

                if (previousStatus != PaymentStatus.Completed)
                {
                    var contact = await _contacts.GetAsync(tenant.OwnerUserId, ct);
                    if (contact is { } c && !string.IsNullOrWhiteSpace(c.Email))
                    {
                        var firstName = c.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                            .FirstOrDefault()
                                        ?? c.DisplayName;
                        var invoiceUrl = string.Empty;
                        await _email.SendTutorPaymentReceiptAsync(
                            c.Email,
                            firstName,
                            payment.Amount,
                            invoiceUrl,
                            ct);
                        // SCHOOL_APPROVED uniquement quand l'école est vraiment ouverte au public
                        if (tenant.Status == TenantStatus.Active)
                        {
                            await _email.SendSchoolApprovedAsync(
                                c.Email,
                                firstName,
                                tenant.Name,
                                $"{_urls.WebBaseUrl}/login/tuteur",
                                ct);
                        }
                    }
                }

                return;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string AppendQuery(string url, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(url))
            return $"?{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    public async Task<MobileMoneyChargeResponse> CreateMobileMoneyChargeAsync(
        CreateMobileMoneyChargeRequest request,
        CancellationToken ct = default)
    {
        var channel = PaymentMethodCodes.Normalize(request.Channel);
        if (PaymentMethodCodes.IsDisabledCollectionChannel(channel))
            throw new InvalidOperationException(PaymentMethodCodes.MobileMoneyCollectionDisabledMessage);
        if (!PaymentMethodCodes.IsMobileMoney(channel))
            throw new InvalidOperationException("Canal invalide. Utilisez une carte bancaire ou PayPal.");

        var subscription = await _db.StudentSubscriptionsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, ct)
            ?? throw new InvalidOperationException("Abonnement introuvable.");

        var offering = await _db.SubscriptionOfferingsForAnyTenant
            .FirstOrDefaultAsync(o => o.Id == subscription.OfferingId, ct)
            ?? throw new InvalidOperationException("Offre d'abonnement introuvable.");

        if (!offering.Currency.Equals("XAF", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le paiement Mobile Money Cameroun exige une offre en XAF.");

        EnsureSubscriptionPayable(subscription, offering);

        var student = await _db.StudentsForAnyTenant
            .FirstOrDefaultAsync(s => s.Id == subscription.StudentId, ct)
            ?? throw new InvalidOperationException("Étudiant introuvable.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct)
            ?? throw new InvalidOperationException("Tuteur introuvable.");

        var parent = await _db.ParentProfilesForAnyTenant.FirstOrDefaultAsync(p => p.Id == student.ParentProfileId, ct)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var customer = await CreateOrGetParentCustomerAsync(parent.Id, ct);
        var amountExclusive = offering.Price;
        var split = await SplitParentPaymentAsync(tenant, amountExclusive, channel, ct);
        var billingCountry = string.IsNullOrWhiteSpace(request.BillingCountryCode)
            ? "CM"
            : request.BillingCountryCode.Trim().ToUpperInvariant();

        var productCode = ToProductCode(offering.Id);
        var planCode = SubscriptionPackRules.ResolvePlanCode(offering.DurationDays);
        await EnsureCatalogItemAsync(offering, productCode, planCode, ct);

        var payment = new Payment
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            Amount = amountExclusive, // mis à jour au TTC après charge
            ProcessorFee = split.ProcessorFee,
            PlatformFee = split.PlatformFee,
            TutorAmount = split.TutorAmount,
            GroupAmount = split.GroupAmount,
            ExpertGroupId = split.ExpertGroupId,
            CommissionPercent = split.CommissionPercent,
            Currency = offering.Currency,
            Status = PaymentStatus.Pending,
            Channel = channel.ToUpperInvariant()
        };
        _db.Add(payment);
        await _db.SaveChangesAsync(ct);

        var fullName = $"{parent.FirstName} {parent.LastName}".Trim();
        var metadata = JsonSerializer.Serialize(new
        {
            payment_id = payment.Id,
            subscription_id = subscription.Id,
            tenant_id = tenant.Id,
            commission_percent = split.CommissionPercent.ToString("0.##"),
            payment_method = channel,
            billing_country = billingCountry
        });

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"mm-{payment.Id:N}"
            : request.IdempotencyKey.Trim();

        var charge = await _gateway.ChargeMobileMoneyAsync(new GatewayMobileMoneyChargeRequest(
            customer.CustomerCode,
            parent.Email,
            fullName,
            parent.UserId,
            productCode,
            planCode,
            channel.ToUpperInvariant(),
            request.PhoneNumber,
            billingCountry,
            metadata,
            $"Abonnement {offering.Title}",
            request.ReturnUrl,
            request.CancelUrl), idempotencyKey, ct);

        payment.StripePaymentIntentId = charge.PaymentCode;
        payment.PhoneMasked = charge.PhoneMasked;
        payment.Channel = charge.Channel;
        payment.Amount = charge.Amount; // TTC réellement encaissé
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Charge Mobile Money créée pour l'abonnement {SubscriptionId} (paymentCode={PaymentCode}, channel={Channel}, HT={Exclusive}, Tax={Tax}, TTC={Inclusive}, country={Country})",
            subscription.Id,
            charge.PaymentCode,
            charge.Channel,
            charge.AmountExclusive,
            charge.TaxAmount,
            charge.Amount,
            charge.BillingCountryCode);

        return new MobileMoneyChargeResponse(
            payment.Id,
            charge.PaymentCode,
            charge.Status,
            charge.Amount,
            charge.Currency,
            charge.Channel,
            charge.PhoneMasked,
            charge.ProviderReference,
            charge.ExpiresAtUtc,
            charge.Instruction,
            charge.UssdHint,
            charge.AmountExclusive,
            charge.TaxAmount,
            charge.TaxRatePercent,
            charge.TaxName,
            charge.BillingCountryCode,
            charge.PaymentUrl);
    }

    public async Task<AfricanTaxQuoteDto> QuoteAfricanTaxAsync(
        decimal amountExclusive,
        string currency,
        string countryCode,
        CancellationToken ct = default)
    {
        var quote = await _gateway.QuoteAfricanTaxAsync(
            new GatewayAfricanTaxQuoteRequest(amountExclusive, currency, countryCode),
            ct);
        return new AfricanTaxQuoteDto(
            quote.CountryCode,
            quote.CountryName,
            quote.TaxName,
            quote.TaxRatePercent,
            quote.AmountExclusive,
            quote.TaxAmount,
            quote.AmountInclusive,
            quote.Currency);
    }

    public async Task<IReadOnlyList<AfricanTaxRateDto>> ListAfricanTaxRatesAsync(CancellationToken ct = default)
    {
        var rates = await _gateway.ListAfricanTaxRatesAsync(ct);
        return rates
            .Select(r => new AfricanTaxRateDto(r.CountryCode, r.CountryName, r.TaxName, r.RatePercent, r.Notes))
            .ToList();
    }
}
