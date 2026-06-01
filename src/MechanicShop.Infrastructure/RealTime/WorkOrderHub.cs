using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

[Authorize]
public sealed class WorkOrderHub : Hub
{
    public const string HubUrl = "/hubs/workOrders";
}
