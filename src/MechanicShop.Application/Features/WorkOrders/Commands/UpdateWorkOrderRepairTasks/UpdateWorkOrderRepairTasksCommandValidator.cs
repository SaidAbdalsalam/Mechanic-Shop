using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed class UpdateWorkOrderRepairTasksCommandValidator
    : AbstractValidator<UpdateWorkOrderRepairTasksCommand>
{
    public UpdateWorkOrderRepairTasksCommandValidator()
    {
        RuleFor(wo => wo.WorkOrderId).NotEmpty().WithMessage("Work order ID is required.");

        RuleFor(wo => wo.RepairTasksIds)
            .NotEmpty()
            .WithMessage("Repair task ID is required, should be at least one.");
    }
}
