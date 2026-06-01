namespace MechanicShop.Application.Features.Invoices.DTOs;

public sealed record InvoiceLineItemDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);
