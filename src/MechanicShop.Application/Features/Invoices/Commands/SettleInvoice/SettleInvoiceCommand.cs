using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed record SettleInvoiceCommand(Guid InvoiceId) : IRequest<Result<Success>>;
