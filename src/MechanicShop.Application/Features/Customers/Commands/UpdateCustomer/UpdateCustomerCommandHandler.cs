using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateCustomerCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateCustomerCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateCustomerCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateCustomerCommand command, CancellationToken ct)
    {
        var email = command.Email.ToLower().Trim();

        var exist = await _context.Customers.AnyAsync(
            c => c.Email == email && c.Id != command.Id,
            ct
        );

        if (exist)
        {
            _logger.LogWarning(
                "Customer update aborted. The email address {Email} is already in use by another customer.",
                email
            );

            return CustomerErrors.EmailAlreadyInUse;
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to update customer. Customer with ID {CustomerId} was not found.",
                command.Id
            );

            return ApplicationErrors.CustomerNotFound;
        }

        var updatedCustomer = customer.Update(
            command.Name.Trim(),
            command.PhoneNumber.Trim(),
            email,
            command.Address.Trim()
        );

        if (updatedCustomer.IsError)
        {
            return updatedCustomer.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Customer {CustomerId} has been updated successfully.", command.Id);

        return Result.Updated;
    }
}
