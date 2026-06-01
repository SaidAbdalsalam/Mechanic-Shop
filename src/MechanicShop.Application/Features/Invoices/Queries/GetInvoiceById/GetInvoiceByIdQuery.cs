using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : ICachedQuery<Result<InvoiceDto>>
{
    public string CacheKey => $"Invoice-{InvoiceId}";

    public string[] Tags => ["invoices"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
