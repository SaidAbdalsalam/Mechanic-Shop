using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.Scheduling.DTOs;

public sealed class AvailabilitySlotDto
{
    public Guid? WorkOrderId { get; set; }
    public Spot Spot { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string? Vehicle { get; set; }
    public EmployeeDto? Labor { get; set; }
    public bool IsOccupied { get; set; }
    public bool? IsAvailable { get; set; }
    public bool WorkOrderLocked { get; set; }
    public WorkOrderState? State { get; set; }
    public RepairTaskDto[]? RepairTasks { get; set; }
}
