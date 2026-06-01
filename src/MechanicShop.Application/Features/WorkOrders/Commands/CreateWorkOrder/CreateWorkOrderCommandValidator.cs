using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.VehicleId).NotEmpty().WithMessage("VehicleId is required.");

        RuleFor(request => request.StartAt)
            .Must((request, startAt) => startAt > timeProvider.GetUtcNow())
            .WithErrorCode("StartAt_Past")
            .WithMessage("StartAt must be in the future.");

        RuleFor(request => request.RepairTaskIds)
            .NotEmpty()
            .WithMessage("At least one repair task must be selected");

        RuleFor(request => request.LaborId)
            .NotEmpty()
            .WithErrorCode("Labor_Required")
            .WithMessage("LaborId is required and must not be empty.");

        RuleFor(x => x.Spot)
            .IsInEnum()
            .WithErrorCode("Spot_Invalid")
            .WithMessage("Spot must be a valid Spot value. [A, B, C, D]");
    }
}
