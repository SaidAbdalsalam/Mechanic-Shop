using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryHandler(
    IAppDbContext context,
    IInvoicePdfGenerator pdfGenerator,
    ILogger<GetInvoicePdfQueryHandler> Logger
) : IRequestHandler<GetInvoicePdfQuery, Result<InvoicePdfDto>>
{
    private readonly IAppDbContext _context = context;

    private readonly ILogger<GetInvoicePdfQueryHandler> _logger = Logger;

    public async Task<Result<InvoicePdfDto>> Handle(GetInvoicePdfQuery query, CancellationToken ct)
    {
        var invoice = await _context
            .Invoices.AsNoTracking()
            .Include(i => i.LineItems)
            .Include(i => i.WorkOrder)
                .ThenInclude(wo => wo!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", query.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        try
        {
            var pdfBytes = await Task.Run(() => pdfGenerator.Generate(invoice), ct);
            var invoicePdf = new InvoicePdfDto
            {
                Content = pdfBytes,
                FileName = $"Invoice-{invoice.Id}.pdf",
            };
            return invoicePdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate PDF for InvoiceId: {InvoiceId}",
                query.InvoiceId
            );
            return Error.Failure("An error occurred while generating the invoice PDF.");
        }
    }
}
