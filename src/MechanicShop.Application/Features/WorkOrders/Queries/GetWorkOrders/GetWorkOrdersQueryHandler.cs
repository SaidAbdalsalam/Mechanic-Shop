using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.DTOs;
using MechanicShop.Application.Features.WorkOrders.DTOs;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public sealed class GetWorkOrdersQueryHandler(IAppDbContext Context)
    : IRequestHandler<GetWorkOrdersQuery, Result<PaginatedList<WorkOrderListItemDto>>>
{
    private readonly IAppDbContext _context = Context;

    public async Task<Result<PaginatedList<WorkOrderListItemDto>>> Handle(
        GetWorkOrdersQuery query,
        CancellationToken ct
    )
    {
        var workOrders = _context.WorkOrders.AsNoTracking().AsQueryable();

        workOrders = ApplyFilters(workOrders, query);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            workOrders = ApplySearch(workOrders, query.SearchTerm);
        }
        workOrders = ApplySort(workOrders, query.SortColumn, query.SortDirection);

        var count = await workOrders.CountAsync(ct);

        var items = await workOrders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(wo => new WorkOrderListItemDto
            {
                WorkOrderId = wo.Id,
                InvoiceId = wo.Invoice == null ? null : wo.Invoice.Id,
                Spot = wo.Spot,
                StartAtUtc = wo.StartAtUtc,
                EndAtUtc = wo.EndAtUtc,
                Vehicle = new VehicleDto(
                    wo.Vehicle!.CustomerId,
                    wo.VehicleId,
                    wo.Vehicle.Make,
                    wo.Vehicle.Model,
                    wo.Vehicle.Year,
                    wo.Vehicle.LicensePlate
                ),
                Customer =
                    wo.Vehicle != null && wo.Vehicle.Customer != null
                        ? wo.Vehicle.Customer.Name
                        : null,
                Labor = wo.Labor != null ? wo.Labor.FirstName + " " + wo.Labor.LastName : null,
                State = wo.State,
                RepairTasks = wo.RepairTasks.Select(rt => rt.Name).ToList(),
            })
            .ToListAsync(ct);

        return new PaginatedList<WorkOrderListItemDto>
        {
            Items = items,
            PageNumber = query.Page,
            PageSize = query.PageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)query.PageSize),
        };
    }

    private IQueryable<WorkOrder> ApplyFilters(
        IQueryable<WorkOrder> workOrders,
        GetWorkOrdersQuery filterQuery
    )
    {
        if (filterQuery.State.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.State == filterQuery.State);
        }
        if (filterQuery.VehicleId.HasValue && filterQuery.VehicleId != Guid.Empty)
        {
            workOrders = workOrders.Where(wo => wo.VehicleId == filterQuery.VehicleId.Value);
        }

        if (filterQuery.LaborId.HasValue && filterQuery.LaborId != Guid.Empty)
        {
            workOrders = workOrders.Where(wo => wo.LaborId == filterQuery.LaborId.Value);
        }

        if (filterQuery.StartDateFrom.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.StartAtUtc >= filterQuery.StartDateFrom.Value);
        }

        if (filterQuery.StartDateTo.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.StartAtUtc <= filterQuery.StartDateTo.Value);
        }

        if (filterQuery.EndDateFrom.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.EndAtUtc >= filterQuery.EndDateFrom.Value);
        }

        if (filterQuery.EndDateTo.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.EndAtUtc <= filterQuery.EndDateTo.Value);
        }

        if (filterQuery.Spot.HasValue)
        {
            workOrders = workOrders.Where(wo => wo.Spot == filterQuery.Spot.Value);
        }
        return workOrders;
    }

    private IQueryable<WorkOrder> ApplySearch(IQueryable<WorkOrder> workOrders, string searchQuery)
    {
        var normalized = searchQuery.Trim().ToLower();

        return workOrders.Where(wo =>
            (
                wo.Vehicle != null
                && (
                    wo.Vehicle.Make.ToLower().Contains(normalized)
                    || wo.Vehicle.Model.ToLower().Contains(normalized)
                    || wo.Vehicle.LicensePlate.ToLower().Contains(normalized)
                )
            )
            || (
                wo.Labor != null
                && (
                    wo.Labor.FirstName.ToLower().Contains(normalized)
                    || wo.Labor.LastName.ToLower().Contains(normalized)
                    || (wo.Labor.FirstName + " " + wo.Labor.LastName).ToLower().Contains(normalized)
                )
            )
            || wo.RepairTasks.Any(rt => rt.Name.ToLower().Contains(normalized))
            || wo.Id.ToString().ToLower().Contains(normalized)
        );
    }

    private IQueryable<WorkOrder> ApplySort(
        IQueryable<WorkOrder> workOrders,
        string columnSort,
        string sortQuery
    )
    {
        var isDescending = sortQuery.Equals("desc", StringComparison.CurrentCultureIgnoreCase);

        return columnSort switch
        {
            "createdat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.CreateAtUtc)
                : workOrders.OrderBy(wo => wo.CreateAtUtc),
            "updatedat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.LastModifiedAtUtc)
                : workOrders.OrderBy(wo => wo.StartAtUtc),
            "startat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.StartAtUtc)
                : workOrders.OrderBy(wo => wo.StartAtUtc),
            "endat" => isDescending
                ? workOrders.OrderByDescending(wo => wo.EndAtUtc)
                : workOrders.OrderBy(wo => wo.EndAtUtc),
            "state" => isDescending
                ? workOrders.OrderByDescending(wo => wo.State)
                : workOrders.OrderBy(wo => wo.State),
            "spot" => isDescending
                ? workOrders.OrderByDescending(wo => wo.Spot)
                : workOrders.OrderBy(wo => wo.Spot),
            "total" => isDescending
                ? workOrders.OrderByDescending(wo => wo.Total)
                : workOrders.OrderBy(wo => wo.Total),
            "vehicleid" => isDescending
                ? workOrders.OrderByDescending(wo => wo.VehicleId)
                : workOrders.OrderBy(wo => wo.VehicleId),
            "laborid" => isDescending
                ? workOrders.OrderByDescending(wo => wo.LaborId)
                : workOrders.OrderBy(wo => wo.LaborId),
            _ => workOrders.OrderByDescending(wo => wo.CreateAtUtc),
        };
    }
}
