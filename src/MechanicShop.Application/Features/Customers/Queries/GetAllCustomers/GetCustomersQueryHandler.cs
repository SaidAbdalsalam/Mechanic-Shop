using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.GetAllCustomers.Queries;

public sealed class GetCustomersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomersQuery, Result<List<CustomerDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken ct
    )
    {
        var customers = await _context
            .Customers.AsNoTracking()
            .Include(c => c.Vehicles)
            .ToListAsync(ct);

        return customers.ToDtos();
    }
}
