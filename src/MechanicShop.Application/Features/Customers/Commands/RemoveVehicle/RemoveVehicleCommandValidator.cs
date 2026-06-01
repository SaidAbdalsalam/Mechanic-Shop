using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandValidator : AbstractValidator<RemoveVehicleCommand>
{
    public RemoveVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Customer Id Is required");
    }
}
