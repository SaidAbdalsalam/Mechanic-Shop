using MechanicShop.Application.Features.RepairTasks.Command.CreateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Command.UpdateRepairTask;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    DateTimeOffset StartAt,
    Guid LaborId,
    Guid VehicleId,
    Spot Spot,
    List<Guid> RepairTaskIds
) : IRequest<Result<WorkOrderDto>>;
