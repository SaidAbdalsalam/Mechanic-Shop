using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdateRepairTaskCommandValidator : AbstractValidator<UpdateRepairTaskCommand>
{
    public UpdateRepairTaskCommandValidator()
    {
        RuleFor(p => p.RepairTaskId).NotEmpty().WithMessage("Id is required");

        RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(r => r.EstimatedDurationInMins)
            .NotEmpty()
            .WithMessage("Duration time is required")
            .IsInEnum();

        RuleFor(r => r.LaborCost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleForEach(r => r.Parts).SetValidator(new UpdatePartsDtoValidator());
    }
}
