using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;

namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed record UpdateRepairTaskCommand(
    Guid RepairTaskId,
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<UpdatePartsDto> Parts
) : IRequest<Result<Updated>>;
