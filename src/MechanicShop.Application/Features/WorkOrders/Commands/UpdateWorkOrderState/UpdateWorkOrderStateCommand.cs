using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderState;

public sealed record UpdateWorkOrderStateCommand(Guid WorkOrderId, WorkOrderState WorkOrderState)
    : IRequest<Result<Updated>>;
