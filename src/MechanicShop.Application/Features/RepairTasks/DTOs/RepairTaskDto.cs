using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Application.Features.RepairTasks.DTOs;

public record RepairTaskDto(
    Guid RepairTaskId,
    string Name,
    RepairDurationInMinutes EstimatedDurationInMins,
    decimal LaborCost,
    List<PartsDto> Parts,
    decimal TotalCost
);
