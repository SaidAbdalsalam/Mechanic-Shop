namespace MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;

public sealed record UpdatePartsDto(Guid? PartId, string Name, decimal Cost, int Quantity);
