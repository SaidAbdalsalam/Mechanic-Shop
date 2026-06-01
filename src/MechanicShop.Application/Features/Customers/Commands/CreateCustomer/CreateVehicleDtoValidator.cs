using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateVehicleDtoValidator : AbstractValidator<CreateVehicleDto>
{
    public CreateVehicleDtoValidator()
    {
        RuleFor(x => x.Make).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Model).NotEmpty().MaximumLength(50);

        RuleFor(x => x.LicensePlate).NotEmpty().MaximumLength(10);
    }
}
