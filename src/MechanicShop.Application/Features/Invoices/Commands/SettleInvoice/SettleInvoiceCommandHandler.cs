using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed class SettleInvoiceCommandHandler(
    IAppDbContext Context,
    ILogger<SettleInvoiceCommandHandler> Logger,
    TimeProvider DateTime,
    HybridCache Cache
) : IRequestHandler<SettleInvoiceCommand, Result<Success>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<SettleInvoiceCommandHandler> _logger = Logger;
    private readonly TimeProvider _dateTime = DateTime;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Success>> Handle(SettleInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(
            i => i.Id == command.InvoiceId,
            ct
        );

        if (invoice is null)
        {
            _logger.LogWarning("Invoice with id: {InvoiceId} no found", command.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }
        var payInvoiceResult = invoice.MarkAsPaid(_dateTime);

        if (payInvoiceResult.IsError)
        {
            _logger.LogWarning(
                "Invoice payment failed for InvoiceId: {InvoiceId}. Errors: {Errors}",
                invoice.Id,
                payInvoiceResult.Errors
            );
            return payInvoiceResult.Errors;
        }
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("invoice", ct);
        _logger.LogInformation("Invoice {InvoiceId} successfully paid.", invoice.Id);
        return Result.Success;
    }
}
