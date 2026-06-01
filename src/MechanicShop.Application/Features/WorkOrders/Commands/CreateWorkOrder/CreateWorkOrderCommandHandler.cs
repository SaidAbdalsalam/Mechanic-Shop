using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IAppDbContext Context,
    ILogger<CreateWorkOrderCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy workOrderValidator,
    TimeProvider TimeProvider
) : IRequestHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<CreateWorkOrderCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderValidator = workOrderValidator;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<WorkOrderDto>> Handle(
        CreateWorkOrderCommand command,
        CancellationToken ct
    )
    {
        var repairTasks = await _context
            .RepairTasks.Where(rt => command.RepairTaskIds.Contains(rt.Id))
            .ToListAsync(ct);

        if (repairTasks.Count != command.RepairTaskIds.Count)
        {
            var missingIds = command
                .RepairTaskIds.Except(repairTasks.Select(rt => rt.Id))
                .ToArray();
            _logger.LogWarning(
                "Some RepairTaskIds not found: {MissingIds}",
                string.Join(", ", missingIds)
            );
            return ApplicationErrors.RepairTaskNotFound;
        }
        var totalEstimatedDuration = TimeSpan.FromMinutes(
            repairTasks.Sum(tr => (int)tr.EstimatedDurationInMins)
        );

        var endAt = command.StartAt.Add(totalEstimatedDuration);

        if (_workOrderValidator.IsOutsideOperatingHours(command.StartAt, endAt))
        {
            _logger.LogWarning(
                "The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.",
                command.StartAt,
                endAt
            );

            return ApplicationErrors.WorkOrderOutsideOperatingHour(command.StartAt, endAt);
        }

        var checkMinRequirementResult = _workOrderValidator.ValidateMinimumRequirement(
            command.StartAt,
            endAt
        );

        if (checkMinRequirementResult.IsError)
        {
            _logger.LogWarning("WorkOrder duration is shorter than the configured minimum.");

            return checkMinRequirementResult.Errors;
        }

        var checkSpotAvailabilityResult = await _workOrderValidator.CheckSpotAvailabilityAsync(
            command.Spot,
            command.StartAt,
            endAt,
            excludeWorkOrderId: null,
            ct
        );

        if (checkSpotAvailabilityResult.IsError)
        {
            _logger.LogWarning("Spot: {Spot} is not available.", command.Spot.ToString());
            return checkSpotAvailabilityResult.Errors;
        }

        var vehicle = await _context
            .Vehicles.Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, ct);

        if (vehicle is null)
        {
            _logger.LogWarning("Vehicle with Id '{VehicleId}' does not exist.", command.VehicleId);

            return ApplicationErrors.VehicleNotFound;
        }

        var labor = await _context.Employees.FirstOrDefaultAsync(
            e => e.Id == command.LaborId && e.Role == Role.Labor,
            ct
        );

        if (labor is null)
        {
            _logger.LogWarning("Invalid LaborId: {LaborId}", command.LaborId.ToString());
            return ApplicationErrors.LaborNotFound;
        }
        var vehicleIsSchedule = await _workOrderValidator.IsVehicleAlreadyScheduled(
            vehicle.Id,
            command.StartAt,
            endAt,
            null
        );

        var laborIsOccupied = await _workOrderValidator.IsLaborOccupied(
            command.LaborId,
            command.StartAt,
            endAt,
            null
        );

        var createWorkOrderResult = WorkOrder.Create(
            Guid.NewGuid(),
            command.VehicleId,
            command.StartAt,
            endAt,
            command.LaborId,
            command.Spot,
            repairTasks,
            _timeProvider
        );

        if (createWorkOrderResult.IsError)
        {
            _logger.LogWarning(
                "Failed to create WorkOrder: {Error}",
                createWorkOrderResult.TopError.Description
            );

            return createWorkOrderResult.Errors;
        }

        var workOrder = createWorkOrderResult.Value;

        _context.WorkOrders.Add(workOrder);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WorkOrder with Id '{WorkOrderId}' created successfully.",
            workOrder.Id
        );

        await _cache.RemoveByTagAsync("work-order", ct);

        return workOrder.ToDto();
    }
}
