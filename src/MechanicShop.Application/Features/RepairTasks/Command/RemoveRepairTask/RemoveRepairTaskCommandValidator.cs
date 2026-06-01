using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;

public sealed class RemoveRepairTaskCommandValidator : AbstractValidator<RemoveRepairTaskCommand>
{
    public RemoveRepairTaskCommandValidator()
    {
        RuleFor(r => r.RepairTaskId).NotEmpty().WithMessage("Id is required");
    }
}
