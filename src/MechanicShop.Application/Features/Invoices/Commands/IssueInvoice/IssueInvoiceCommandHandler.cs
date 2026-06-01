using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Application.Features.Invoices.Mapper;
using MechanicShop.Domain.Common.Constraints;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed class IssueInvoiceCommandHandler(
    IAppDbContext Context,
    ILogger<IssueInvoiceCommandHandler> Logger,
    TimeProvider Datetime,
    HybridCache Cache
) : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<IssueInvoiceCommandHandler> _logger = Logger;
    private readonly TimeProvider _datetime = Datetime;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand command, CancellationToken ct)
    {
        var workOrder = await _context
            .WorkOrders.Include(wo => wo.Vehicle)
                .ThenInclude(v => v!.Customer)
            .Include(wo => wo.Invoice)
            .Include(wo => wo.RepairTasks)
                .ThenInclude(wo => wo.Parts)
            .FirstOrDefaultAsync(wo => wo.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogWarning("WorkOrder with id: {WorkOrderId} no found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }
        if (workOrder.State != WorkOrderState.Completed)
        {
            _logger.LogWarning(
                "WorkOrder with id: {WorkOrderId} is not completed",
                command.WorkOrderId
            );
            return ApplicationErrors.WorkOrderMustBeCompletedForInvoicing;
        }

        var invoiceId = Guid.NewGuid();
        var lineItems = new List<InvoiceLineItem>();
        var lineNumber = 1;

        foreach (var (task, taskIndex) in workOrder.RepairTasks.Select((t, i) => (t, i + 1)))
        {
            var partsSummary = task.Parts.Any()
                ? string.Join(
                    Environment.NewLine,
                    task.Parts.Select(p => $"• {p.Name} x {p.Quantity} @ {p.Cost:C}")
                )
                : "No parts";

            var lineDescription =
                $"{taskIndex}: {task.Name}{Environment.NewLine}"
                + $"  Labor = {task.LaborCost:C}{Environment.NewLine}"
                + $"  Parts:{Environment.NewLine}{partsSummary}";

            var totalPartsCost = task.Parts.Sum(p => p.Quantity * p.Cost);

            var totalTaskCost = task.LaborCost + totalPartsCost;

            var lineItemResult = InvoiceLineItem.Create(
                invoiceId,
                lineNumber++,
                lineDescription,
                1,
                totalTaskCost
            );

            if (lineItemResult.IsError)
            {
                return lineItemResult.Errors;
            }

            lineItems.Add(lineItemResult.Value);
        }

        var discountAmount = command.Discount ?? 0m;

        var createInvoiceResult = Invoice.Create(
            id: invoiceId,
            workOrderId: workOrder.Id,
            items: lineItems,
            discountAmount: discountAmount,
            taxRate: MechanicShopConstraints.TaxRate,
            datetime: _datetime,
            laborCost: workOrder.RepairTasks.Sum(rt => rt.LaborCost),
            partsCost: workOrder
                .RepairTasks.SelectMany(rt => rt.Parts)
                .Sum(p => p.Cost * p.Quantity)
        );
        if (createInvoiceResult.IsError)
        {
            _logger.LogWarning(
                "Invoice creation failed for WorkOrderId: {WorkOrderId}. Errors: {@Errors}",
                command.WorkOrderId,
                createInvoiceResult.Errors
            );
            return createInvoiceResult.Errors;
        }

        var invoice = createInvoiceResult.Value;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("invoice", ct);
        _logger.LogInformation("invoice with id: {InvoiceId} added successfully", invoiceId);

        return invoice.ToDto();
    }
}
