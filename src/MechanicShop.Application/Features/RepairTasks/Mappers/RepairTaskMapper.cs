using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.RepairTasks;

namespace MechanicShop.Application.Features.RepairTasks.Mappers;

public static class RepairTaskMapper
{
    public static RepairTaskDto ToDto(this RepairTask repairTask)
    {
        ArgumentNullException.ThrowIfNull(repairTask);
        return new RepairTaskDto(
            repairTask.Id,
            repairTask.Name,
            repairTask.EstimatedDurationInMins,
            repairTask.LaborCost,
            repairTask.Parts.Select(p => p.ToDto()).ToList() ?? [],
            repairTask.TotalCost
        );
    }

    public static PartsDto ToDto(this Part part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return new PartsDto(part.Name, part.Cost, part.Quantity);
    }

    public static List<RepairTaskDto> ToDtos(this IEnumerable<RepairTask> repairTasks)
    {
        return [.. repairTasks.Select(p => p.ToDto())];
    }

    public static IEnumerable<PartsDto> ToDtos(this IEnumerable<Part> parts)
    {
        return [.. parts.Select(p => p.ToDto())];
    }
}
