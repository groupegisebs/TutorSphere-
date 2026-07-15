using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Invoices;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceDto>> GetAllAsync(CancellationToken ct = default);
    Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default);
    Task<InvoiceDto> SendAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> MarkPaidAsync(Guid id, CancellationToken ct = default);

    /// <summary>Crée / lie une facture au paiement (idempotent). Retourne l'invoice id.</summary>
    Task<Guid> EnsureInvoiceForPaymentAsync(Guid paymentId, CancellationToken ct = default);

    Task<(byte[] Content, string FileName)?> BuildInvoicePdfForParentAsync(
        string parentUserId,
        Guid paymentId,
        CancellationToken ct = default);
}

public class InvoiceService : IInvoiceService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _email;
    private readonly IBillingEmailOrchestrator _billingEmail;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IEmailService email,
        IBillingEmailOrchestrator billingEmail,
        ILogger<InvoiceService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
        _billingEmail = billingEmail;
        _logger = logger;
    }

    public Task<IReadOnlyList<InvoiceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var invoices = _db.Invoices
            .OrderByDescending(i => i.IssuedAt)
            .ToList()
            .Select(MapToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<InvoiceDto>>(invoices);
    }

    public Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = _db.Invoices.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(invoice is null ? null : MapToDto(invoice));
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var invoiceNumber = GenerateInvoiceNumber();

        var invoice = new Invoice
        {
            TenantId = tenantId,
            ParentProfileId = request.ParentProfileId,
            InvoiceNumber = invoiceNumber,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = PaymentStatus.Pending,
            IssuedAt = DateTime.UtcNow
        };

        _db.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return MapToDto(invoice);
    }

    public async Task<InvoiceDto> SendAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = _db.Invoices.FirstOrDefault(i => i.Id == id)
            ?? throw new InvalidOperationException("Facture introuvable.");

        var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == invoice.ParentProfileId);

        if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
        {
            try
            {
                var invoiceUrl = $"/invoices/{invoice.Id}";
                await _email.SendInvoiceReadyAsync(parent.Email, parent.FirstName, invoiceUrl, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec d'envoi de la facture {InvoiceId} au parent {ParentId}", id, invoice.ParentProfileId);
            }
        }

        return MapToDto(invoice);
    }

    public async Task<InvoiceDto> MarkPaidAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = _db.Invoices.FirstOrDefault(i => i.Id == id)
            ?? throw new InvalidOperationException("Facture introuvable.");

        var wasPaid = invoice.Status == PaymentStatus.Completed;
        invoice.Status = PaymentStatus.Completed;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (!wasPaid)
        {
            var payment = _db.PaymentsForAnyTenant.FirstOrDefault(p => p.InvoiceId == invoice.Id);
            if (payment is not null)
            {
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt ??= invoice.PaidAt;
                payment.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await _billingEmail.NotifyPaymentSucceededAsync(payment.Id, ct);
            }
            else
            {
                var parent = _db.ParentProfiles.FirstOrDefault(p => p.Id == invoice.ParentProfileId);
                if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
                {
                    try
                    {
                        await _email.SendParentPaymentReceiptAsync(
                            parent.Email,
                            parent.FirstName,
                            "Élève",
                            invoice.Amount,
                            $"/invoices/{invoice.Id}",
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Échec reçu paiement facture {InvoiceId}", id);
                    }
                }
            }
        }

        return MapToDto(invoice);
    }

    public async Task<Guid> EnsureInvoiceForPaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = _db.PaymentsForAnyTenant.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new InvalidOperationException("Paiement introuvable.");

        if (payment.InvoiceId.HasValue)
            return payment.InvoiceId.Value;

        Guid? parentProfileId = null;
        string? offeringTitle = null;
        if (payment.SubscriptionId is Guid subId)
        {
            var subscription = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subId);
            if (subscription is not null)
            {
                var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == subscription.StudentId);
                parentProfileId = student?.ParentProfileId;
                var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == subscription.OfferingId);
                offeringTitle = offering?.Title;
            }
        }

        if (parentProfileId is not Guid parentId)
            throw new InvalidOperationException("Impossible de rattacher une facture : parent introuvable.");

        var invoice = new Invoice
        {
            TenantId = payment.TenantId,
            ParentProfileId = parentId,
            InvoiceNumber = GenerateInvoiceNumber(),
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            IssuedAt = payment.CreatedAt == default ? DateTime.UtcNow : payment.CreatedAt,
            PaidAt = payment.CompletedAt,
            StripeInvoiceId = string.IsNullOrWhiteSpace(offeringTitle) ? null : offeringTitle
        };

        _db.Add(invoice);
        await _db.SaveChangesAsync(ct);

        payment.InvoiceId = invoice.Id;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return invoice.Id;
    }

    public async Task<(byte[] Content, string FileName)?> BuildInvoicePdfForParentAsync(
        string parentUserId,
        Guid paymentId,
        CancellationToken ct = default)
    {
        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId);
        if (parent is null)
            return null;

        var payment = _db.PaymentsForAnyTenant.FirstOrDefault(p => p.Id == paymentId);
        if (payment is null)
            return null;

        // Ownership: payment must belong to one of the parent's children subscriptions
        if (payment.SubscriptionId is not Guid subId)
            return null;

        var subscription = _db.StudentSubscriptionsForAnyTenant.FirstOrDefault(s => s.Id == subId);
        if (subscription is null)
            return null;

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == subscription.StudentId);
        if (student is null || student.ParentProfileId != parent.Id)
            return null;

        if (!payment.InvoiceId.HasValue)
            await EnsureInvoiceForPaymentAsync(payment.Id, ct);

        payment = _db.PaymentsForAnyTenant.FirstOrDefault(p => p.Id == paymentId)!;
        var invoice = _db.InvoicesForAnyTenant.FirstOrDefault(i => i.Id == payment.InvoiceId)
            ?? throw new InvalidOperationException("Facture introuvable.");

        var offering = _db.SubscriptionOfferingsForAnyTenant.FirstOrDefault(o => o.Id == subscription.OfferingId);
        var tutor = _db.Tenants.FirstOrDefault(t => t.Id == payment.TenantId);
        var studentName = $"{student.FirstName} {student.LastName}".Trim();
        var parentName = $"{parent.FirstName} {parent.LastName}".Trim();
        var description = offering?.Title ?? invoice.StripeInvoiceId ?? "Abonnement TutorSphere";
        var statusLabel = payment.Status switch
        {
            PaymentStatus.Completed => "Payé",
            PaymentStatus.Pending => "En attente",
            PaymentStatus.Failed => "Échoué",
            PaymentStatus.Refunded => "Remboursé",
            _ => payment.Status.ToString()
        };

        var pdf = InvoicePdfGenerator.Generate(
            invoice.InvoiceNumber,
            parentName,
            studentName,
            tutor?.Name,
            description,
            payment.Amount,
            payment.Currency,
            invoice.IssuedAt,
            payment.CompletedAt ?? invoice.PaidAt,
            statusLabel);

        var fileName = $"Facture-{invoice.InvoiceNumber}.pdf";
        return (pdf, fileName);
    }

    private Guid RequireTenantId()
    {
        if (!_tenantContext.HasTenant || _tenantContext.TenantId is null)
            throw new InvalidOperationException("Contexte locataire requis.");
        return _tenantContext.TenantId.Value;
    }

    private static string GenerateInvoiceNumber()
        => $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static InvoiceDto MapToDto(Invoice i) => new(
        i.Id,
        i.InvoiceNumber,
        i.ParentProfileId,
        i.Amount,
        i.Currency,
        i.Status.ToString(),
        i.IssuedAt,
        i.PaidAt);
}
