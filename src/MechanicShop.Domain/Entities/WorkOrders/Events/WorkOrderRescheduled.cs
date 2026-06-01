using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderRescheduled : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
