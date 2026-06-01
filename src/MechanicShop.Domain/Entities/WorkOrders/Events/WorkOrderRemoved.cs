using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderRemoved : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
