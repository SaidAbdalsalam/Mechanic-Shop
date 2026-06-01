using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.EventHandlers;

public sealed class WorkOrderDashboardNotifierHandler(IWorkOrderNotifier notifier)
    : INotificationHandler<WorkOrderCreated>,
        INotificationHandler<WorkOrderCompleted>,
        INotificationHandler<WorkOrderCancelled>,
        INotificationHandler<WorkOrderRescheduled>,
        INotificationHandler<WorkOrderSpotUpdated>,
        INotificationHandler<WorkOrderLaborReassigned>,
        INotificationHandler<WorkOrderRepairTasksUpdated>,
        INotificationHandler<WorkOrderRemoved>
{
    private readonly IWorkOrderNotifier _notifier = notifier;

    public Task Handle(WorkOrderCreated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCreated), ct);

    public Task Handle(WorkOrderCompleted notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCompleted), ct);

    public Task Handle(WorkOrderCancelled notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderCancelled), ct);

    public Task Handle(WorkOrderRescheduled notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRescheduled), ct);

    public Task Handle(WorkOrderSpotUpdated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderSpotUpdated), ct);

    public Task Handle(WorkOrderLaborReassigned notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderLaborReassigned), ct);

    public Task Handle(WorkOrderRepairTasksUpdated notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRepairTasksUpdated), ct);

    public Task Handle(WorkOrderRemoved notification, CancellationToken ct) =>
        NotifyAsync(notification.WorkOrderId, nameof(WorkOrderRemoved), ct);

    private Task NotifyAsync(Guid workOrderId, string eventType, CancellationToken ct)
    {
        return _notifier.NotifyWorkOrdersChangedAsync(workOrderId, eventType, ct);
    }
}
