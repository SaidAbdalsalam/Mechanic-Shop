using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryByIdHandler(
    IAppDbContext context,
    ILogger<GetEmployeesQueryByIdHandler> logger
) : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetEmployeesQueryByIdHandler> _logger = logger;

    public async Task<Result<EmployeeDto>> Handle(
        GetEmployeeByIdQuery request,
        CancellationToken ct
    )
    {
        var employee = await _context
            .Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);

        if (employee is null)
        {
            _logger.LogWarning("Employee with id: {Id} not found", request.Id);
            return ApplicationErrors.EmployeeNotFound;
        }
        return employee.ToDto();
    }
}
