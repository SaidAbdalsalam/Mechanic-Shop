using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed class CreateRepairTaskCommandValidator : AbstractValidator<CreateRepairTaskCommand>
{
    public CreateRepairTaskCommandValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(r => r.EstimatedDurationInMins)
            .NotEmpty()
            .WithMessage("Duration time is required")
            .IsInEnum();

        RuleFor(r => r.LaborCost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleFor(r => r.Parts).NotNull().WithMessage("Parts is required");

        RuleForEach(r => r.Parts).SetValidator(new CreatePartsDtoValidator());
    }
}
