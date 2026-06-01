using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Commands;

public sealed class CreateEmployeeCommandHandler(
    IAppDbContext context,
    ILogger<CreateEmployeeCommandHandler> logger,
    HybridCache cache,
    IIdentityService identityService
) : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateEmployeeCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IIdentityService _identityService = identityService;

    public async Task<Result<EmployeeDto>> Handle(
        CreateEmployeeCommand command,
        CancellationToken ct
    )
    {
        var identityResult = await _identityService.CreateUserAsync(
            command.Email,
            command.Password,
            command.Role.ToString()
        );

        if (identityResult.IsError)
        {
            _logger.LogWarning(
                "Failed to create identity for employee with email: {Email}.",
                command.Email
            );
            return identityResult.Errors;
        }

        var userId = identityResult.Value;

        var employeeResult = Employee.Create(
            userId,
            command.FirstName,
            command.LastName,
            command.Role
        );

        if (employeeResult.IsError)
        {
            await _identityService.DeleteUserAsync(userId.ToString());
            return employeeResult.Errors;
        }

        var employee = employeeResult.Value;

        try
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Database save failed for Employee {EmployeeId}. Deleting Identity account.",
                employee.Id
            );
            await _identityService.DeleteUserAsync(userId.ToString());
            throw;
        }

        _logger.LogInformation("Employee with id: {EmployeeId} added successfully", employee.Id);

        await _cache.RemoveByTagAsync("employee", ct);

        return employee.ToDto();
    }
}
