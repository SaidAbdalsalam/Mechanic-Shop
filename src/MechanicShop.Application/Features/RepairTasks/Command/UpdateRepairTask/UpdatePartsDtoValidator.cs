using FluentValidation;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdatePartsDtoValidator : AbstractValidator<UpdatePartsDto>
{
    public UpdatePartsDtoValidator()
    {
        RuleFor(p => p.PartId).NotEmpty().WithMessage("Id is required");

        RuleFor(p => p.Name).NotEmpty().WithMessage("Name is required").MaximumLength(50);

        RuleFor(p => p.Cost).GreaterThan(0).WithMessage("Cost must be greater than 0");

        RuleFor(p => p.Quantity)
            .InclusiveBetween(1, 10)
            .WithMessage("Quantity must be between 1 and 10");
    }
}
