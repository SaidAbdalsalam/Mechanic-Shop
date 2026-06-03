using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed class UpdateWorkOrderRepairTasksCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateWorkOrderRepairTasksCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy WorkOrderPolicy,
    TimeProvider TimeProvider
) : IRequestHandler<UpdateWorkOrderRepairTasksCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateWorkOrderRepairTasksCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<Updated>> Handle(
        UpdateWorkOrderRepairTasksCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context
            .WorkOrders.Include(wo => wo.RepairTasks)
            .FirstOrDefaultAsync(wo => wo.Id == command.WorkOrderId);

        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        if (command.RepairTasksIds.Length == 0)
        {
            _logger.LogError("Empty RepairTaskIds list submitted.");

            return RepairTaskErrors.AtLeastOneRepairTaskIsRequired;
        }

        var repairTasks = await _context
            .RepairTasks.Where(t => command.RepairTasksIds.Contains(t.Id))
            .ToListAsync(ct);

        if (repairTasks.Count != command.RepairTasksIds.Count())
        {
            var missingIds = command
                .RepairTasksIds.Except(repairTasks.Select(rt => rt.Id))
                .ToArray();
            _logger.LogWarning(
                "Some RepairTaskIds not found: {MissingIds}",
                string.Join(", ", missingIds)
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var startAt = workOrder.StartAtUtc;
        var duration = TimeSpan.FromMinutes(repairTasks.Sum(rt => (int)rt.EstimatedDurationInMins));
        var endAt = startAt.Add(duration);

        if (_workOrderPolicy.IsOutsideOperatingHours(startAt, endAt))
        {
            _logger.LogWarning(
                "The WorkOrder time ({StartAt} ? {EndAt}) is outside of store operating hours.",
                startAt,
                endAt
            );
            return ApplicationErrors.WorkOrderOutsideOperatingHour(startAt, endAt);
        }

        var addRepairTaskResult = workOrder.UpdateRepairTasks(repairTasks);
        if (addRepairTaskResult.IsError)
        {
            return addRepairTaskResult;
        }

        var spotCheckResult = await _workOrderPolicy.CheckSpotAvailabilityAsync(
            workOrder.Spot,
            workOrder.StartAtUtc,
            endAt,
            excludeWorkOrderId: workOrder.Id,
            ct: ct
        );

        if (spotCheckResult.IsError)
        {
            return spotCheckResult.Errors;
        }

        var isLaborOccupied = await _workOrderPolicy.IsLaborOccupied(
            workOrder.LaborId,
            workOrder.StartAtUtc,
            endAt,
            workOrder.Id
        );

        if (isLaborOccupied)
            return ApplicationErrors.LaborOccupied;

        var timingResult = workOrder.UpdateTiming(workOrder.StartAtUtc, endAt, _timeProvider);

        if (timingResult.IsError)
        {
            return timingResult.Errors;
        }
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Repair tasks added successfully");
        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}
