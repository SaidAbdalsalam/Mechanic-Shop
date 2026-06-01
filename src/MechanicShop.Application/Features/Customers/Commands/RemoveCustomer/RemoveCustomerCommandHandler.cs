using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed class RemoveCustomerCommandHandler(
    IAppDbContext Context,
    ILogger<RemoveCustomerCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RemoveCustomerCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand command, CancellationToken ct)
    {
        var customer = await _context.Customers.FindAsync([command.Id], ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to delete customer. Customer with ID {CustomerId} was not found.",
                command.Id
            );
            return ApplicationErrors.CustomerNotFound;
        }

        var vehicleUnderWork = await _context.WorkOrders.AnyAsync(
            wo => wo.Vehicle!.CustomerId == command.Id,
            ct
        );

        if (vehicleUnderWork)
        {
            _logger.LogWarning(
                "Cannot delete customer {CustomerId}. They have associated work orders.",
                command.Id
            );
            return CustomerErrors.CannotDeleteCustomerWithWorkOrders;
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Customer {CustomerId} has been deleted successfully.", command.Id);

        return Result.Deleted;
    }
}
