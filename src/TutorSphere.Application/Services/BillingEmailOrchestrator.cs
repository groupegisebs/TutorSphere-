using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IBillingEmailOrchestrator
{
    Task NotifyEnrollmentRequestedAsync(Guid subscriptionId, CancellationToken ct = default);
    Task NotifyEnrollmentAcceptedAsync(Guid subscriptionId, CancellationToken ct = default);
    Task NotifyPaymentSucceededAsync(Guid paymentId, CancellationToken ct = default);
    Task NotifyPaymentLinkReadyAsync(Guid subscriptionId, string checkoutUrl, decimal amount, CancellationToken ct = default);
}

/// <summary>Centralise les e-mails inscription cours / paiement parent & tuteur.</summary>
public sealed class BillingEmailOrchestrator : IBillingEmailOrchestrator
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly IUserContactLookup _contacts;
    private readonly IAppUrlProvider _urls;
    private readonly ILogger<BillingEmailOrchestrator> _logger;

    public BillingEmailOrchestrator(
        IApplicationDbContext db,
        IEmailService email,
        IUserContactLookup contacts,
        IAppUrlProvider urls,
        ILogger<BillingEmailOrchestrator> logger)
    {
        _db = db;
        _email = email;
        _contacts = contacts;
        _urls = urls;
        _logger = logger;
    }

    private string WebBase => _urls.WebBaseUrl;

    public async Task NotifyEnrollmentRequestedAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        try
        {
            var ctx = ResolveSubscription(subscriptionId);
            if (ctx is null) return;

            var tenant = _db.Tenants.FirstOrDefault(t => t.Id == ctx.Value.Sub.TenantId);
            if (tenant is null || string.IsNullOrWhiteSpace(tenant.OwnerUserId)) return;

            var tutor = await _contacts.GetAsync(tenant.OwnerUserId, ct);
            if (tutor is null) return;

            await _email.SendCourseEnrollmentRequestAsync(
                tutor.Value.Email,
                tutor.Value.DisplayName,
                ctx.Value.StudentName,
                ctx.Value.CourseTitle,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec e-mail demande d'inscription {SubscriptionId}", subscriptionId);
        }
    }

    public async Task NotifyEnrollmentAcceptedAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        try
        {
            var ctx = ResolveSubscription(subscriptionId);
            if (ctx is null || ctx.Value.Parent is null || string.IsNullOrWhiteSpace(ctx.Value.Parent.Email))
                return;

            var needsPay = ctx.Value.Sub.Status == SubscriptionStatus.AwaitingPayment;
            var note = needsPay
                ? "Votre inscription est acceptée. Veuillez procéder au paiement pour activer les cours."
                : "Votre inscription est acceptée et active. Les cours seront planifiés prochainement.";
            var url = needsPay
                ? $"{WebBase}/parent/subscriptions"
                : $"{WebBase}/parent/calendar";

            await _email.SendCourseEnrollmentAcceptedAsync(
                ctx.Value.Parent.Email,
                ctx.Value.Parent.FirstName,
                ctx.Value.StudentName,
                ctx.Value.CourseTitle,
                note,
                url,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec e-mail acceptation inscription {SubscriptionId}", subscriptionId);
        }
    }

    public async Task NotifyPaymentLinkReadyAsync(
        Guid subscriptionId,
        string checkoutUrl,
        decimal amount,
        CancellationToken ct = default)
    {
        try
        {
            var ctx = ResolveSubscription(subscriptionId);
            if (ctx?.Parent is null || string.IsNullOrWhiteSpace(ctx.Value.Parent.Email))
                return;

            // Lien de paiement (pas un reçu — le reçu part au succès).
            await _email.SendInvoiceReadyAsync(
                ctx.Value.Parent.Email,
                ctx.Value.Parent.FirstName,
                string.IsNullOrWhiteSpace(checkoutUrl) ? $"{WebBase}/parent/subscriptions" : checkoutUrl,
                ct);

            _logger.LogInformation(
                "Lien de paiement e-maillé au parent pour {SubscriptionId} ({Amount})",
                subscriptionId,
                amount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec e-mail lien de paiement {SubscriptionId}", subscriptionId);
        }
    }

    public async Task NotifyPaymentSucceededAsync(Guid paymentId, CancellationToken ct = default)
    {
        try
        {
            var payment = _db.PaymentsForAnyTenant.FirstOrDefault(p => p.Id == paymentId);
            if (payment is null || payment.Status != PaymentStatus.Completed)
                return;

            if (payment.SubscriptionId is not Guid subId)
                return;

            var ctx = ResolveSubscription(subId);
            if (ctx is null) return;

            var invoiceUrl = payment.InvoiceId is Guid inv
                ? $"{WebBase}/parent/invoices/{inv}"
                : $"{WebBase}/parent/subscriptions";

            if (ctx.Value.Parent is not null && !string.IsNullOrWhiteSpace(ctx.Value.Parent.Email))
            {
                await _email.SendParentPaymentReceiptAsync(
                    ctx.Value.Parent.Email,
                    ctx.Value.Parent.FirstName,
                    ctx.Value.StudentName,
                    payment.Amount,
                    invoiceUrl,
                    ct);
            }

            var tenant = _db.Tenants.FirstOrDefault(t => t.Id == ctx.Value.Sub.TenantId);
            if (tenant is not null && !string.IsNullOrWhiteSpace(tenant.OwnerUserId))
            {
                var tutor = await _contacts.GetAsync(tenant.OwnerUserId, ct);
                if (tutor is not null)
                {
                    await _email.SendTutorStudentPaymentReceivedAsync(
                        tutor.Value.Email,
                        tutor.Value.DisplayName,
                        ctx.Value.StudentName,
                        ctx.Value.CourseTitle,
                        payment.Amount,
                        ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec e-mails paiement réussi {PaymentId}", paymentId);
        }
    }

    private (Domain.Entities.StudentSubscription Sub, string StudentName, string CourseTitle, Domain.Entities.ParentProfile? Parent)? ResolveSubscription(Guid subscriptionId)
    {
        var sub = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subscriptionId);
        if (sub is null) return null;

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sub.StudentId);
        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == sub.OfferingId);
        var parent = student?.ParentProfileId is Guid pid
            ? _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.Id == pid)
            : null;

        return (
            sub,
            student is null ? "Élève" : $"{student.FirstName} {student.LastName}".Trim(),
            offering?.Title ?? "Cours",
            parent);
    }
}
