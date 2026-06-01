using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Role Role
) : IRequest<Result<EmployeeDto>>;
