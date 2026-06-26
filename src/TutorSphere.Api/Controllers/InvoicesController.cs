using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorSphere.Application.DTOs.Invoices;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService) => _invoiceService = invoiceService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> List(CancellationToken ct)
        => Ok(await _invoiceService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid id, CancellationToken ct)
    {
        var invoice = await _invoiceService.GetByIdAsync(id, ct);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        try
        {
            var invoice = await _invoiceService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<InvoiceDto>> Send(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _invoiceService.SendAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<InvoiceDto>> MarkPaid(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _invoiceService.MarkPaidAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
