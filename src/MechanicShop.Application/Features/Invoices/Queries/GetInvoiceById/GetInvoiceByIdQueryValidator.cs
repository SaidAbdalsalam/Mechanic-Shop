using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryValidator : AbstractValidator<GetInvoiceByIdQuery>
{
    public GetInvoiceByIdQueryValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}
