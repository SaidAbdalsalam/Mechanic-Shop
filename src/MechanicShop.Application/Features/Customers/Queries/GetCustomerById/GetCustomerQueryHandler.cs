using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery Query, CancellationToken ct)
    {
        var customer = await _context
            .Customers.Include(c => c.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == Query.CustomerId, ct);

        if (customer is null)
        {
            return ApplicationErrors.CustomerNotFound;
        }

        return customer.ToDto();
    }
}
