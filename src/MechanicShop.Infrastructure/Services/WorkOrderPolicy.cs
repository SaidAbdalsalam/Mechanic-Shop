using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MechanicShop.Infrastructure.Services;

public sealed class WorkOrderPolicy(IOptions<AppSettings> options, IAppDbContext context)
    : IWorkOrderPolicy
{
    private readonly AppSettings _appSettings = options.Value;
    private readonly IAppDbContext _context = context;

    public async Task<Result<Success>> CheckSpotAvailabilityAsync(
        Spot spot,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludeWorkOrderId = null,
        CancellationToken ct = default
    )
    {
        var isOccupied = await _context.WorkOrders.AnyAsync(
            a =>
                a.Spot == spot
                && a.StartAtUtc < endAt
                && a.EndAtUtc > startAt
                && (!excludeWorkOrderId.HasValue || a.Id != excludeWorkOrderId.Value),
            ct
        );

        return isOccupied
            ? Error.Conflict(
                "MechanicShop_Spot_Full",
                "The selected time slot is unavailable for the requested services."
            )
            : Result.Success;
    }

    public Result<Success> ValidateMinimumRequirement(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (
            (endAt - startAt)
            < TimeSpan.FromMinutes(_appSettings.MinimumAppointmentDurationInMinutes)
        )
        {
            return Error.Conflict(
                "WorkOrder_TooShort",
                $"WorkOrder duration must be at least {_appSettings.MinimumAppointmentDurationInMinutes} minutes."
            );
        }

        return Result.Success;
    }

    public async Task<bool> IsLaborOccupied(
        Guid laborId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    )
    {
        return await _context.WorkOrders.AnyAsync(a =>
            a.LaborId == laborId
            && a.Id != excludedWorkOrderId
            && a.StartAtUtc < endAt
            && a.EndAtUtc > startAt
        );
    }

    public bool IsOutsideOperatingHours(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(_appSettings.StoreTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }
        var localStartAt = TimeZoneInfo.ConvertTime(startAt, timeZone);
        var localEndAt = TimeZoneInfo.ConvertTime(endAt, timeZone);

        if (localStartAt.Date != localEndAt.Date)
        {
            return true;
        }
        var startTimeOnly = TimeOnly.FromTimeSpan(localStartAt.TimeOfDay);
        var endTimeOnly = TimeOnly.FromTimeSpan(localEndAt.TimeOfDay);

        if (startTimeOnly < _appSettings.OpeningTime || endTimeOnly > _appSettings.ClosingTime)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> IsVehicleAlreadyScheduled(
        Guid vehicleId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? excludedWorkOrderId = null
    )
    {
        return await _context.WorkOrders.AnyAsync(a =>
            a.VehicleId == vehicleId
            && (excludedWorkOrderId == null || a.Id != excludedWorkOrderId)
            && a.StartAtUtc < endAt
            && a.EndAtUtc > startAt
        );
    }
}
