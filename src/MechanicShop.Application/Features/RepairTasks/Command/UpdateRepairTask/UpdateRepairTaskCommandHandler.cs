using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed class UpdateRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<UpdateRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<UpdateRepairTaskCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<UpdateRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<Updated>> Handle(UpdateRepairTaskCommand command, CancellationToken ct)
    {
        var nameExists = await _context.RepairTasks.AnyAsync(
            rt => rt.Name.ToLower() == command.Name.ToLower() && rt.Id != command.RepairTaskId,
            ct
        );

        if (nameExists)
        {
            _logger.LogWarning("Repair task with name: {Name} already exists", command.Name);
            return RepairTaskErrors.NameAlreadyExists;
        }

        var repairTask = await _context
            .RepairTasks.Include(R => R.Parts)
            .FirstOrDefaultAsync(r => r.Id == command.RepairTaskId, ct);

        if (repairTask is null)
        {
            _logger.LogWarning(
                "Repair task with id: {RepairTaskId} not found",
                command.RepairTaskId
            );
            return ApplicationErrors.RepairTaskNotFound;
        }

        var validatedParts = new List<Part>();
        foreach (var p in command.Parts)
        {
            var partId = p.PartId ?? Guid.NewGuid();

            var partResult = Part.Create(partId, p.Name, p.Cost, p.Quantity);

            if (partResult.IsError)
            {
                return partResult.Errors;
            }

            validatedParts.Add(partResult.Value);
        }

        var resultUpdated = repairTask.Update(
            command.Name,
            command.EstimatedDurationInMins,
            command.LaborCost
        );

        if (resultUpdated.IsError)
        {
            return resultUpdated.Errors;
        }

        var upsertPartsResult = repairTask.UpsertParts(validatedParts);

        if (upsertPartsResult.IsError)
        {
            return upsertPartsResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("repair-task", ct);

        return Result.Updated;
    }
}
