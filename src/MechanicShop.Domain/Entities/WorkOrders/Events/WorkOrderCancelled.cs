using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderCancelled : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
