using FluentValidation;
using MechanicShop.Application.Features.Invoices.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryValidator : AbstractValidator<GetInvoicePdfQuery>
{
    public GetInvoicePdfQueryValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}
