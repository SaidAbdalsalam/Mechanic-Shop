using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler(
    IAppDbContext context,
    ILogger<GetInvoiceByIdQueryHandler> Logger
) : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;

    private readonly ILogger<GetInvoiceByIdQueryHandler> _logger = Logger;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery query, CancellationToken ct)
    {
        var invoice = await _context
            .Invoices.AsNoTracking() //
            .Include(i => i.LineItems) //
            .Include(i => i.WorkOrder) //
                .ThenInclude(wo => wo!.Vehicle)
                    .ThenInclude(v => v!.Customer)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", query.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        return invoice.ToDto();
    }
}
