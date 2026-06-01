using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public sealed record UpdateWorkOrderRepairTasksCommand(Guid WorkOrderId, Guid[] RepairTasksIds)
    : IRequest<Result<Updated>>;
