using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed class UpdateWorkOrderStateCommandValidator
    : AbstractValidator<UpdateWorkOrderStateCommand>
{
    public UpdateWorkOrderStateCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty().WithMessage("Work order id is required.");

        RuleFor(x => x.WorkOrderState)
            .IsInEnum()
            .WithErrorCode("WorkOrderStatus_Invalid")
            .WithMessage("Status must be a valid WorkOrderStatus value.");
    }
}
