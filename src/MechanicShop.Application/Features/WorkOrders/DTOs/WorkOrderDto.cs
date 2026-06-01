using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.DTOs;

public sealed class WorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Spot Spot { get; set; }
    public VehicleDto? Vehicle { get; set; }
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }
    public List<RepairTaskDto> RepairTasks { get; set; } = [];
    public EmployeeDto? Labor { get; set; }
    public WorkOrderState State { get; set; }
    public decimal TotalPartCost { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalCost { get; set; }
    public int TotalDurationInMins { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
