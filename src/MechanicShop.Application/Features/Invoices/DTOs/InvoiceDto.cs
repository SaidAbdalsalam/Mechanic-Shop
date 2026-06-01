using MechanicShop.Application.Features.Customers.DTOs;

namespace MechanicShop.Application.Features.Invoices.DTOs;

public sealed record InvoiceDto(
    Guid InvoiceId,
    Guid WorkOrderId,
    DateTimeOffset IssuedAtUtc,
    CustomerDto? Customer,
    VehicleDto? Vehicle,
    decimal? DiscountAmount,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    string? PaymentStatus,
    List<InvoiceLineItemDto> Items
);
