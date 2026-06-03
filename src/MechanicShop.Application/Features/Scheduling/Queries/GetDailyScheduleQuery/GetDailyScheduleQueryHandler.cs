using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Mapper;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.Scheduling.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;

public sealed class GetDailyScheduleQueryHandler(IAppDbContext Context, TimeProvider TimeProvider)
    : IRequestHandler<GetDailyScheduleQuery, Result<ScheduleDto>>
{
    private readonly IAppDbContext _context = Context;

    private readonly TimeProvider _timeProvider = TimeProvider;

    public async Task<Result<ScheduleDto>> Handle(GetDailyScheduleQuery query, CancellationToken ct)
    {
        var localStart = query.ScheduleDate.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, query.TimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, query.TimeZone);

        var now = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), query.TimeZone);

        var workOrders = await _context
            .WorkOrders.AsNoTracking()
            .Where(wo =>
                wo.StartAtUtc < endUtc
                && wo.EndAtUtc > startUtc
                && (query.LaborId == null || wo.LaborId == query.LaborId)
            )
            .Include(w => w.RepairTasks)
            .Include(w => w.Vehicle)
            .Include(w => w.Labor)
            .ToListAsync(ct);

        var result = new ScheduleDto(query.ScheduleDate, now < endUtc, []);

        foreach (var spot in Enum.GetValues<Spot>())
        {
            var current = localStart;
            var slots = new List<AvailabilitySlotDto>();

            var woBySpot = workOrders
                .Where(w => w.Spot == spot)
                .OrderBy(w => w.StartAtUtc)
                .ToList();

            while (current < endUtc)
            {
                var next = current.AddMinutes(15);
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(current, query.TimeZone);
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(next, query.TimeZone);
                var workOrder = woBySpot.FirstOrDefault(wo =>
                    wo.StartAtUtc < utcEnd && wo.EndAtUtc > utcStart
                );

                if (workOrder != null)
                {
                    if (!slots.Any(s => s.WorkOrderId == workOrder.Id))
                    {
                        slots.Add(
                            new AvailabilitySlotDto
                            {
                                WorkOrderId = workOrder.Id,
                                Spot = spot,
                                StartAt = workOrder.StartAtUtc,
                                EndAt = workOrder.EndAtUtc,
                                Vehicle = FormatVehicleInfo(workOrder.Vehicle!),
                                Labor = workOrder.Labor!.ToDto(),
                                IsOccupied = true,
                                RepairTasks =
                                [
                                    .. workOrder.RepairTasks.ToList().ConvertAll(rt => rt.ToDto()),
                                ],
                                WorkOrderLocked = !workOrder.IsEditable,
                                State = workOrder.State,
                                IsAvailable = false,
                            }
                        );
                    }
                }
                else
                {
                    slots.Add(
                        new AvailabilitySlotDto
                        {
                            Spot = spot,
                            StartAt = utcStart,
                            EndAt = utcEnd,
                            WorkOrderLocked = false,
                            IsOccupied = false,
                            IsAvailable = current >= now,
                        }
                    );
                }
                current = next;
            }
            result.Spots.Add(new SpotDto(spot, slots));
        }
        return result;
    }

    private static string? FormatVehicleInfo(Vehicle vehicle) =>
        vehicle != null ? $"{vehicle.Make} | {vehicle.LicensePlate}" : null;
}
