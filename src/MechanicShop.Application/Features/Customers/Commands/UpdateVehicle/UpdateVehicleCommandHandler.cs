using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateVehicle;

public sealed class UpdateVehicleCommandHandler(
    IAppDbContext context,
    ILogger<UpdateVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateVehicleCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateVehicleCommand command, CancellationToken ct)
    {
        var licensePlate = command.LicensePlate.Trim();
        var make = command.Make.Trim();
        var model = command.Model.Trim();

        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.Id == command.VehicleId,
            ct
        );

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with id: {VehicleId} is not found", command.VehicleId);
            return ApplicationErrors.VehicleNotFound;
        }

        var isLicensePlateUsed = await _context.Vehicles.AnyAsync(
            v => v.LicensePlate == licensePlate && v.Id != command.VehicleId,
            ct
        );

        if (isLicensePlateUsed)
        {
            _logger.LogWarning(
                "Failed to update vehicle {VehicleId}. License plate {LicensePlate} is already in use by another vehicle.",
                command.VehicleId,
                licensePlate
            );
            return ApplicationErrors.VehicleAlreadyExists;
        }

        var vehicleResult = vehicle.Update(make, model, command.Year, licensePlate);

        if (vehicleResult.IsError)
        {
            return vehicleResult.Errors;
        }

        await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync("customer", ct);

        _logger.LogInformation("Vehicle {VehicleId} updated successfully.", command.VehicleId);

        return Result.Updated;
    }
}
