using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed class CreateRepairTaskCommandHandler(
    IAppDbContext Context,
    ILogger<CreateRepairTaskCommandHandler> Logger,
    HybridCache Cache
) : IRequestHandler<CreateRepairTaskCommand, Result<RepairTaskDto>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<CreateRepairTaskCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;

    public async Task<Result<RepairTaskDto>> Handle(
        CreateRepairTaskCommand command,
        CancellationToken ct
    )
    {
        var repairTaskExist = await _context.RepairTasks.AnyAsync(
            c => c.Name.ToLower() == command.Name.ToLower(),
            ct
        );
        if (repairTaskExist)
        {
            _logger.LogWarning(
                "Repair task with name: {RepairTaskName} is already exists",
                command.Name
            );
            return RepairTaskErrors.NameAlreadyExists;
        }

        var parts = new List<Part>();

        foreach (var p in command.Parts)
        {
            var part = Part.Create(Guid.NewGuid(), p.Name, p.Cost, p.Quantity);
            if (part.IsError)
            {
                return part.Errors;
            }
            parts.Add(part.Value);
        }

        var duplicateGroup = command
            .Parts.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateGroup is not null)
        {
            var duplicatePartName = duplicateGroup.First().Name;

            _logger.LogWarning(
                "There is a duplicate in parts: {DuplicatePartName}",
                duplicatePartName
            );

            return RepairTaskErrors.DuplicateName;
        }
        var repairTaskResult = RepairTask.Create(
            Guid.NewGuid(),
            command.Name,
            command.EstimatedDurationInMins,
            command.LaborCost,
            parts
        );

        if (repairTaskResult.IsError)
        {
            return repairTaskResult.Errors;
        }

        var repairTask = repairTaskResult.Value;

        _context.RepairTasks.Add(repairTask);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Repair task with id {RepairTaskId} added successfully",
            repairTask.Id
        );

        await _cache.RemoveByTagAsync("repair-task", ct);

        return repairTask.ToDto();
    }
}
