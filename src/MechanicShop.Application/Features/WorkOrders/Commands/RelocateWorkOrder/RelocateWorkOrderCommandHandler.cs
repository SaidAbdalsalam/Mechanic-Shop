using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;

public sealed class RelocateWorkOrderCommandHandler(
    IAppDbContext Context,
    ILogger<RelocateWorkOrderCommandHandler> Logger,
    HybridCache Cache,
    TimeProvider TimeProvider,
    IWorkOrderPolicy WorkOrderPolicy
) : IRequestHandler<RelocateWorkOrderCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RelocateWorkOrderCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;

    public async Task<Result<Updated>> Handle(
        RelocateWorkOrderCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context
            .WorkOrders.Include(a => a.RepairTasks)
            .Include(a => a.Labor)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var duration = TimeSpan.FromMinutes(
            workOrder.RepairTasks.Sum(rt => (int)rt.EstimatedDurationInMins)
        );
        var endAt = command.NewStartAt.Add(duration);

        var spotIsAvailable = await _workOrderPolicy.CheckSpotAvailabilityAsync(
            command.NewSpot,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );
        if (spotIsAvailable.IsError)
        {
            _logger.LogWarning("Spot: {Spot} is not available.", workOrder.Spot.ToString());
            return spotIsAvailable.Errors;
        }
        var isLaborOccupied = await _workOrderPolicy.IsLaborOccupied(
            workOrder.LaborId,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );

        if (isLaborOccupied)
            return ApplicationErrors.LaborOccupied;

        var vehicleIsScheduled = await _workOrderPolicy.IsVehicleAlreadyScheduled(
            workOrder.VehicleId,
            command.NewStartAt,
            endAt,
            command.WorkOrderId
        );

        if (vehicleIsScheduled)
            return ApplicationErrors.VehicleSchedulingConflict;

        var updateTimingResult = workOrder.UpdateTiming(command.NewStartAt, endAt, _timeProvider);
        if (updateTimingResult.IsError)
        {
            _logger.LogError(
                "Failed to update timing: {Error}",
                updateTimingResult.TopError.Description
            );

            return updateTimingResult.Errors;
        }
        var updateSpotResult = workOrder.UpdateSpot(command.NewSpot);

        if (updateSpotResult.IsError)
        {
            _logger.LogError(
                "Failed to update Spot: {Error}",
                updateSpotResult.TopError.Description
            );

            return updateTimingResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);

        return Result.Updated;
    }
}
