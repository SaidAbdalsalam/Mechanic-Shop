using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Common.Interfaces;

public interface IWorkOrderPolicy
{
    bool IsOutsideOperatingHours(DateTimeOffset startAt, DateTimeOffset endAt);

    Task<bool> IsLaborOccupied(
        Guid laborId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    );

    Task<bool> IsVehicleAlreadyScheduled(
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    );

    Task<Result<Success>> CheckSpotAvailabilityAsync(
        Spot spot,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludeWorkOrderId = null,
        CancellationToken ct = default
    );

    Result<Success> ValidateMinimumRequirement(DateTimeOffset startAt, DateTimeOffset endAt);
}
