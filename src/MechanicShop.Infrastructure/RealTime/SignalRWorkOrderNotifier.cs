using MechanicShop.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

public sealed class SignalRWorkOrderNotifier(IHubContext<WorkOrderHub> hubContext)
    : IWorkOrderNotifier
{
    private readonly IHubContext<WorkOrderHub> _hubContext = hubContext;

    public Task NotifyWorkOrdersChangedAsync(
        Guid workOrderId,
        string eventType,
        CancellationToken ct = default
    ) =>
        _hubContext.Clients.All.SendAsync(
            "WorkOrdersChanged",
            new { WorkOrderId = workOrderId, EventType = eventType },
            cancellationToken: ct
        );
}
