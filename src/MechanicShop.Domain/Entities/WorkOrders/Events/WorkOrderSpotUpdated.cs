using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderSpotUpdated : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
