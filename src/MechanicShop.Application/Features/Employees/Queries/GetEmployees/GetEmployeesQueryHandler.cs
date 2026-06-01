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

public sealed class GetEmployeesQueryHandler(
    IAppDbContext context,
    ILogger<GetEmployeesQueryHandler> logger
) : IRequestHandler<GetEmployeesQuery, Result<List<EmployeeDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetEmployeesQueryHandler> _logger = logger;

    public async Task<Result<List<EmployeeDto>>> Handle(
        GetEmployeesQuery request,
        CancellationToken ct
    )
    {
        var query = _context.Employees.AsNoTracking().AsQueryable();

        if (request.Role.HasValue)
        {
            query = query.Where(e => e.Role == request.Role.Value);
        }
        var employees = await query.ToListAsync(ct);

        return employees.ToDtos();
    }
}
