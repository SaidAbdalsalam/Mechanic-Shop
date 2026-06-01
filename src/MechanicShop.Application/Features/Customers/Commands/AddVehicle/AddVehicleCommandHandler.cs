using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicle;

public sealed class AddVehicleCommandHandler(
    IAppDbContext context,
    ILogger<AddVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<AddVehicleCommand, Result<VehicleDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<AddVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<VehicleDto>> Handle(AddVehicleCommand command, CancellationToken ct)
    {
        var customer = await _context
            .Customers.Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);

        if (customer is null)
        {
            _logger.LogWarning(
                "Failed to add vehicle. Customer with ID {CustomerId} not found.",
                command.CustomerId
            );
            return ApplicationErrors.CustomerNotFound;
        }

        var isLicensePlateUsed = await _context.Vehicles.AnyAsync(v =>
            v.LicensePlate == command.LicensePlate
        );

        if (isLicensePlateUsed)
        {
            _logger.LogWarning(
                "Failed to add vehicle. License plate {LicensePlate} is already in use.",
                command.LicensePlate
            );

            return ApplicationErrors.VehicleAlreadyExists;
        }

        var vehicleResult = Vehicle.Create(
            Guid.NewGuid(),
            command.Make,
            command.Model,
            command.Year,
            command.LicensePlate
        );

        if (vehicleResult.IsError)
        {
            return vehicleResult.Errors;
        }

        var addVehicleResult = customer.AddVehicle(vehicleResult.Value);

        if (addVehicleResult.IsError)
        {
            return addVehicleResult.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation(
            "Vehicle with ID {VehicleId} was successfully added to Customer {CustomerId}.",
            vehicleResult.Value.Id,
            customer.Id
        );

        return vehicleResult.Value.ToDto();
    }
}
