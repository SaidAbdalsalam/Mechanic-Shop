using MechanicShop.Application.Features.Customers.Mapper;
using MechanicShop.Application.Features.Employees.DTOs;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.Mappers;

public static class WorkOrderMapper
{
    public static WorkOrderDto ToDto(this WorkOrder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkOrderDto
        {
            WorkOrderId = entity.Id,
            Spot = entity.Spot,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            Labor = entity.Labor is null
                ? null
                : new EmployeeDto
                {
                    Id = entity.LaborId,
                    FirstName = entity.Labor.FirstName,
                    LastName = entity.Labor.LastName,
                    FullName = $"{entity.Labor.FirstName} {entity.Labor.LastName}",
                },
            RepairTasks = entity.RepairTasks.ToDtos(),
            Vehicle = entity.Vehicle is null ? null : entity.Vehicle.ToDto(),
            State = entity.State,
            TotalPartCost = entity
                .RepairTasks.SelectMany(t => t.Parts)
                .Sum(p => p.Cost * p.Quantity),
            TotalLaborCost = entity.RepairTasks.Sum(p => p.LaborCost),
            TotalCost = entity.RepairTasks.Sum(rt => rt.TotalCost),
            TotalDurationInMins = entity.RepairTasks.Sum(rt => (int)rt.EstimatedDurationInMins),
            InvoiceId = entity.Invoice?.Id,
            CreatedAt = entity.CreateAtUtc,
        };
    }

    public static IEnumerable<WorkOrderDto> ToDtos(this IEnumerable<WorkOrder> entities)
    {
        return [.. entities.Select(e => e.ToDto())];
    }

    public static WorkOrderListItemDto ToListItemDto(this WorkOrder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkOrderListItemDto
        {
            WorkOrderId = entity.Id,
            Spot = entity.Spot,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            Vehicle = entity.Vehicle!.ToDto(),
            Labor = entity.Labor is null
                ? null
                : $"{entity.Labor.FirstName} {entity.Labor.LastName}",
            State = entity.State,
            RepairTasks = entity.RepairTasks.Select(rt => rt.Name).ToList(),
            Customer = entity.Vehicle!.Customer!.Name,
            InvoiceId = entity.Invoice == null ? null : entity.Invoice.Id,
        };
    }
}
