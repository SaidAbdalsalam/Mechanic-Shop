using System.Data;
using FluentValidation;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(e => e.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(50);

        RuleFor(e => e.LastName).NotEmpty().WithMessage("Last name is required").MaximumLength(50);

        RuleFor(e => e.Role)
            .IsInEnum()
            .WithMessage("Role name is required and must be a valid role.");

        RuleFor(e => e.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format");

        RuleFor(e => e.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters");
    }
}
