using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.RemoveRepairTask;

public sealed class RemoveRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<RemoveRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<RemoveRepairTaskCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<RemoveRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Deleted>> Handle(RemoveRepairTaskCommand command, CancellationToken ct)
    {
        var repairTask = await _context.RepairTasks.FindAsync([command.RepairTaskId], ct);

        if (repairTask is null)
        {
            _logger.LogWarning(
                "RepairTask {RepairTaskId} not found for deletion.",
                command.RepairTaskId
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var isInUse = await _context
            .WorkOrders.AsNoTracking()
            .SelectMany(wo => wo.RepairTasks)
            .AnyAsync(rt => rt.Id == command.RepairTaskId, ct);

        if (isInUse)
        {
            _logger.LogWarning(
                "RepairTask {RepairTaskId} cannot be deleted — in use by work orders.",
                command.RepairTaskId
            );

            return RepairTaskErrors.InUse;
        }

        _context.RepairTasks.Remove(repairTask);
        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("repair-task", ct);

        _logger.LogInformation(
            "RepairTask {RepairTaskId} deleted successfully.",
            command.RepairTaskId
        );

        return Result.Deleted;
    }
}
