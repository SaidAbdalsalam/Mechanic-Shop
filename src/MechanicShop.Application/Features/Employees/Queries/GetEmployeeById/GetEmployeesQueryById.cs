using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed record GetEmployeeByIdQuery(Guid Id) : ICachedQuery<Result<EmployeeDto>>
{
    public string CacheKey => $"employee-{Id}";

    public string[] Tags => ["employee"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
