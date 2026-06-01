using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed record IssueInvoiceCommand(Guid WorkOrderId, decimal? Discount)
    : IRequest<Result<InvoiceDto>>;
