using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Features.Customers.GetAllCustomers.Queries;

public sealed record GetCustomersQuery : ICachedQuery<Result<List<CustomerDto>>>
{
    public string CacheKey => "customers";

    public string[] Tags => ["customer"];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
