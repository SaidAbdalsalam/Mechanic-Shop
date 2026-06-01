using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed class UpdateWorkOrderStateCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateWorkOrderStateCommandHandler> Logger,
    HybridCache Cache,
    TimeProvider TimeProvider
) : IRequestHandler<UpdateWorkOrderStateCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateWorkOrderStateCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<Updated>> Handle(
        UpdateWorkOrderStateCommand command,
        CancellationToken ct
    )
    {
        var workOrder = await _context.WorkOrders.FindAsync([command.WorkOrderId], ct);
        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        if (
            command.WorkOrderState != WorkOrderState.Cancelled
            && workOrder.StartAtUtc > _timeProvider.GetUtcNow()
        )
        {
            _logger.LogWarning(
                "State transition for WorkOrder Id '{WorkOrderId}` is not allowed before the work order�s scheduled start time.",
                command.WorkOrderId
            );

            return WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc);
        }

        var updateStatusResult = workOrder.UpdateState(command.WorkOrderState);

        if (updateStatusResult.IsError)
        {
            _logger.LogError(
                "Failed to update status: {Error}",
                updateStatusResult.TopError.Description
            );
            return updateStatusResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}
