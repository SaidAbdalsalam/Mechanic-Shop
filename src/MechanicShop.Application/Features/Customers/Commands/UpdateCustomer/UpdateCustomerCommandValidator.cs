using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required!");

        RuleFor(x => x.Name).NotEmpty().WithMessage("Id is required!").MaximumLength(100);

        RuleFor(x => x.Address).NotEmpty().WithMessage("Address is required!").MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^\+?\d{7,15}$")
            .WithMessage("Phone number must be 7–15 digits and may start with '+'.");

        RuleFor(x => x.Email).EmailAddress().WithMessage("Invalid email");
    }
}
