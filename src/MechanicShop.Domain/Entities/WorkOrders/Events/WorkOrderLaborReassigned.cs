using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.WorkOrders.Events;

public sealed class WorkOrderLaborReassigned : DomainEvent
{
    public Guid WorkOrderId { get; init; }
}
