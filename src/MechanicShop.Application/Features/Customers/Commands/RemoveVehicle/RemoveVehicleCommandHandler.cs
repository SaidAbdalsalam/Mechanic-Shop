using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandHandler(
    IAppDbContext context,
    ILogger<RemoveVehicleCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveVehicleCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveVehicleCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveVehicleCommand command, CancellationToken ct)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.Id == command.VehicleId,
            ct
        );

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with id: {VehicleId} is not found", command.VehicleId);
            return ApplicationErrors.VehicleNotFound;
        }

        var vehicleUnderWork = await _context.WorkOrders.AnyAsync(
            wo => wo.VehicleId == command.VehicleId,
            ct
        );

        if (vehicleUnderWork)
        {
            _logger.LogWarning(
                "Cannot delete vehicle {VehicleId}. They have associated work orders.",
                command.VehicleId
            );
            return VehicleErrors.CannotDeleteVehicleWithWorkOrders;
        }

        _context.Vehicles.Remove(vehicle);

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("customer", ct);
        _logger.LogInformation("Vehicle {VehicleId} deleted successfully.", command.VehicleId);
        return Result.Deleted;
    }
}
