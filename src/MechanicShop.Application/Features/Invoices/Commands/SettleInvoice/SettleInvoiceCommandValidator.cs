using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Commands.SettleInvoice;

public sealed class SettleInvoiceCommandValidator : AbstractValidator<SettleInvoiceCommand>
{
    public SettleInvoiceCommandValidator()
    {
        RuleFor(i => i.InvoiceId).NotEmpty().WithMessage("Invoice id is required");
    }
}
