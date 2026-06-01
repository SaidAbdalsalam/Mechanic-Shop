using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;

public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Vehicle Id Is required");

        RuleFor(x => x.Make).NotEmpty().WithMessage("Make Is required").MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().WithMessage("Model Is required").MaximumLength(50);

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .WithMessage("License Plate Is required")
            .MaximumLength(10);
    }
}
