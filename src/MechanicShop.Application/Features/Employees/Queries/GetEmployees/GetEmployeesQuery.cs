using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed record GetEmployeesQuery(Role? Role) : ICachedQuery<Result<List<EmployeeDto>>>
{
    public string CacheKey => $"employees-{Role?.ToString() ?? "all"}";

    public string[] Tags => ["employee"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
