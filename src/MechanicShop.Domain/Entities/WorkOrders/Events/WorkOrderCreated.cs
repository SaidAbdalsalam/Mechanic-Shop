using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderCreated : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
