using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicle;

public sealed class AddVehicleCommandValidator : AbstractValidator<AddVehicleCommand>
{
    public AddVehicleCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer Id Is required");

        RuleFor(x => x.Make).NotEmpty().WithMessage("Make Is required").MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().WithMessage("Model Is required").MaximumLength(50);

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .WithMessage("License Plate Is required")
            .MaximumLength(50);
    }
}
