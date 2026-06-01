namespace MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;

public sealed record CreatePartsDto(string Name, decimal Cost, int Quantity);
