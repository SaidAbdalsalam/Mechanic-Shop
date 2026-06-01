namespace MechanicShop.Application.Common.Interfaces;

public interface IWorkOrderNotifier
{
    Task NotifyWorkOrdersChangedAsync(
        Guid workOrderId,
        string eventType,
        CancellationToken ct = default
    );
}
