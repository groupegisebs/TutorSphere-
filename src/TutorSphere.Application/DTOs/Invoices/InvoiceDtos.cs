namespace TutorSphere.Application.DTOs.Invoices;

public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid ParentProfileId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime IssuedAt,
    DateTime? PaidAt);

public record CreateInvoiceRequest(
    Guid ParentProfileId,
    decimal Amount,
    string Currency = "CAD");
