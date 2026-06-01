using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders.Events;

namespace MechanicShop.Domain.WorkOrders;

public sealed class WorkOrder : AuditableEntity
{
    public Guid VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public Guid LaborId { get; private set; }
    public Employee? Labor { get; private set; }
    public Invoice? Invoice { get; private set; }
    public Spot Spot { get; private set; }
    public DateTimeOffset StartAtUtc { get; private set; }
    public DateTimeOffset EndAtUtc { get; private set; }
    public WorkOrderState State { get; private set; } = WorkOrderState.Scheduled;
    private readonly List<RepairTask> _repairTasks = [];
    public IReadOnlyList<RepairTask> RepairTasks => _repairTasks.AsReadOnly();
    public bool IsEditable => State == WorkOrderState.Scheduled;
    public decimal? TotalPartsCost =>
        _repairTasks.SelectMany(rt => rt.Parts).Sum(p => p.Cost * p.Quantity);
    public decimal? TotalLaborCost => _repairTasks.Sum(rt => rt.LaborCost);
    public decimal? Total => (TotalPartsCost ?? 0) + (TotalLaborCost ?? 0);

    private WorkOrder() { }

    private WorkOrder(
        Guid id,
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid laborId,
        Spot spot,
        List<RepairTask> repairTasks
    )
        : base(id)
    {
        VehicleId = vehicleId;
        StartAtUtc = startAt;
        EndAtUtc = endAt;
        LaborId = laborId;
        Spot = spot;
        _repairTasks = repairTasks;
    }

    public static Result<WorkOrder> Create(
        Guid id,
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid laborId,
        Spot spot,
        List<RepairTask> repairTasks,
        TimeProvider timeProvider
    )
    {
        if (id == Guid.Empty)
        {
            return WorkOrderErrors.WorkOrderIdRequired;
        }
        if (vehicleId == Guid.Empty)
        {
            return WorkOrderErrors.VehicleIdRequired;
        }
        if (laborId == Guid.Empty)
        {
            return WorkOrderErrors.LaborIdRequired;
        }

        if (repairTasks == null || repairTasks.Count == 0)
        {
            return WorkOrderErrors.RepairTasksRequired;
        }
        if (startAt < timeProvider.GetUtcNow())
        {
            return WorkOrderErrors.StartDateInPast;
        }

        if (endAt <= startAt)
        {
            return WorkOrderErrors.InvalidEndDate;
        }
        if (!Enum.IsDefined(typeof(Spot), spot))
        {
            return WorkOrderErrors.SpotInvalid;
        }
        var workOrder = new WorkOrder(id, vehicleId, startAt, endAt, laborId, spot, repairTasks);
        workOrder.AddDomainEvent(new WorkOrderCreated { WorkOrderId = id });
        return workOrder;
    }

    public Result<Updated> UpdateRepairTasks(List<RepairTask> newTasks)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (newTasks is null || newTasks.Count == 0)
        {
            return WorkOrderErrors.RepairTasksRequired;
        }

        _repairTasks.Clear();
        foreach (var task in newTasks)
        {
            if (!_repairTasks.Any(x => x.Id == task.Id))
            {
                _repairTasks.Add(task);
            }
        }

        AddDomainEvent(new WorkOrderRepairTasksUpdated { WorkOrderId = Id });

        return Result.Updated;
    }

    public Result<Updated> UpdateTiming(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeProvider timeProvider
    )
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.TimingReadonly(Id.ToString(), State);
        }
        if (startTime < timeProvider.GetUtcNow())
        {
            return WorkOrderErrors.StartDateInPast;
        }
        if (endTime <= startTime)
        {
            return WorkOrderErrors.InvalidEndDate;
        }

        StartAtUtc = startTime;
        EndAtUtc = endTime;
        AddDomainEvent(new WorkOrderRescheduled() { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Updated> UpdateLabor(Guid laborId)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (laborId == Guid.Empty)
        {
            return WorkOrderErrors.LaborIdEmpty(Id.ToString());
        }

        LaborId = laborId;
        AddDomainEvent(new WorkOrderLaborReassigned() { WorkOrderId = Id });
        return Result.Updated;
    }

    public bool CanTransitionTo(WorkOrderState newState)
    {
        return (State, newState) switch
        {
            (WorkOrderState.Scheduled, WorkOrderState.InProgress) => true,
            (WorkOrderState.InProgress, WorkOrderState.Completed) => true,
            (_, WorkOrderState.Cancelled) when State != WorkOrderState.Completed => true,
            _ => false,
        };
    }

    public Result<Updated> UpdateState(WorkOrderState newState)
    {
        if (!CanTransitionTo(newState))
        {
            return WorkOrderErrors.InvalidStateTransition(State, newState);
        }

        State = newState;

        if (State == WorkOrderState.Completed)
        {
            AddDomainEvent(new WorkOrderCompleted { WorkOrderId = Id });
        }

        return Result.Updated;
    }

    public Result<Updated> Cancel()
    {
        if (!CanTransitionTo(WorkOrderState.Cancelled))
        {
            return WorkOrderErrors.InvalidStateTransition(State, WorkOrderState.Cancelled);
        }

        State = WorkOrderState.Cancelled;
        AddDomainEvent(new WorkOrderCancelled { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Updated> UpdateSpot(Spot newSpot)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.Readonly;
        }

        if (!Enum.IsDefined(typeof(Spot), newSpot))
        {
            return WorkOrderErrors.SpotInvalid;
        }

        Spot = newSpot;
        AddDomainEvent(new WorkOrderSpotUpdated() { WorkOrderId = Id });
        return Result.Updated;
    }

    public Result<Deleted> Delete()
    {
        if (State is not WorkOrderState.Scheduled)
        {
            return WorkOrderErrors.Readonly;
        }

        AddDomainEvent(new WorkOrderRemoved { WorkOrderId = Id });

        return Result.Deleted;
    }
}
