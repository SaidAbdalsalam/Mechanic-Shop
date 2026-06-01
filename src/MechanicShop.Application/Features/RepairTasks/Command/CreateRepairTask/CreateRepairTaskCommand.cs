using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;

namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed record CreateRepairTaskCommand(
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<CreatePartsDto> Parts
) : IRequest<Result<RepairTaskDto>>;
