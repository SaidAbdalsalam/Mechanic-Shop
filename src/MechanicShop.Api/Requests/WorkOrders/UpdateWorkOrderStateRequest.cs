using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Api.Requests.WorkOrders;

public class UpdateWorkOrderStateRequest
{
    public WorkOrderState State { get; set; }
}
