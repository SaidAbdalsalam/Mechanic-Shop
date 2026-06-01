using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

namespace MechanicShop.Application.Features.Invoices.Mapper;

public static class InvoiceMapper
{
    public static InvoiceDto ToDto(this Invoice entity)
    {
        return new InvoiceDto(
            entity.Id,
            entity.WorkOrderId,
            entity.IssuedAtUtc,
            entity.WorkOrder!.Vehicle!.Customer!.ToDto(),
            entity.WorkOrder.Vehicle.ToDto(),
            entity.DiscountAmount,
            entity.Subtotal,
            entity.TaxRate,
            entity.Total,
            entity.Status.ToString(),
            entity.LineItems.ToDtos()
        );
    }

    public static List<InvoiceDto> ToDtos(this IEnumerable<Invoice> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }

    public static InvoiceLineItemDto ToDto(this InvoiceLineItem entity)
    {
        return new InvoiceLineItemDto(
            entity.Description,
            entity.Quantity,
            entity.UnitPrice,
            entity.LineTotal
        );
    }

    public static List<InvoiceLineItemDto> ToDtos(this IEnumerable<InvoiceLineItem> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }
}
