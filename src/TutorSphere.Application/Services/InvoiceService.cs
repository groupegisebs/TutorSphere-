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
}

public class InvoiceService : IInvoiceService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _email;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IEmailService email,
        ILogger<InvoiceService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _email = email;
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

        invoice.Status = PaymentStatus.Completed;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(invoice);
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
