using FluentValidation;

namespace MechanicShop.Application.Features.Invoices.Commands.IssueInvoice;

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(i => i.WorkOrderId).NotEmpty().WithMessage("Work order id is required");
    }
}
