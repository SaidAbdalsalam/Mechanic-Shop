using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed record GetInvoicePdfQuery(Guid InvoiceId) : ICachedQuery<Result<InvoicePdfDto>>
{
    public string CacheKey => $"InvoicePdf-{InvoiceId}";

    public string[] Tags => ["invoices"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
