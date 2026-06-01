using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats;

public sealed class GetWorkOrderStatsQueryHandler(IAppDbContext Context)
    : IRequestHandler<GetWorkOrderStatsQuery, Result<TodayWorkOrderStatsDto>>
{
    private readonly IAppDbContext _context = Context;

    public async Task<Result<TodayWorkOrderStatsDto>> Handle(
        GetWorkOrderStatsQuery request,
        CancellationToken ct
    )
    {
        var localStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, request.TimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, request.TimeZone);

        var query = _context
            .WorkOrders.AsNoTracking()
            .Where(wo => wo.StartAtUtc >= utcStart && wo.StartAtUtc < utcEnd);


        var totalWorkOrders = await query.CountAsync(ct);

        if (totalWorkOrders == 0)
        {
            return new TodayWorkOrderStatsDto
            {
                Date = request.Date,
                Total = 0,
                Scheduled = 0,
                InProgress = 0,
                Completed = 0,
                Cancelled = 0,
                TotalRevenue = 0,
                TotalPartsCost = 0,
                TotalLaborCost = 0,
                UniqueVehicles = 0,
                UniqueCustomers = 0,
                NetProfit = 0,
                ProfitMargin = 0,
                CompletionRate = 0,
                AverageRevenuePerOrder = 0,
                OrdersPerVehicle = 0,
                PartsCostRatio = 0,
                LaborCostRatio = 0,
                CancellationRate = 0,
            };
        }

        var stats = await query.GroupBy(x => 1)
        .Select(g => new
        {
            Scheduled = g.Count(wo => wo.State == WorkOrderState.Scheduled),
            InProgress = g.Count(wo => wo.State == WorkOrderState.InProgress),
            Completed = g.Count(wo => wo.State == WorkOrderState.Completed),
            Cancelled = g.Count(wo => wo.State == WorkOrderState.Cancelled),
        }).FirstOrDefaultAsync(ct);

       var financialTotals = await query
       .Where(wo => wo.Invoice != null)
       .Select(wo => new
       {
           LaborCost = wo.Invoice!.ActualLaborCost,
           PartsCost = wo.Invoice.ActualPartsCost,
           DiscountAmount = wo.Invoice!.DiscountAmount ?? 0m,
           TaxRate = wo.Invoice!.TaxRate,
           GrossSubtotal = wo.Invoice!.LineItems.Sum(li => li.Quantity * li.UnitPrice)
       })
       .GroupBy(wo => 1)
       .Select(g => new
       {
           TotalRevenue = g.Sum(x => x.GrossSubtotal * (1 + x.TaxRate)),
           TotalPartsCost = g.Sum(x => x.PartsCost),
           TotalLaborCost = g.Sum(x => x.LaborCost),
           TotalTax = g.Sum(x => (x.GrossSubtotal - x.DiscountAmount) * x.TaxRate),
       })
       .FirstOrDefaultAsync(ct);

        var totalRevenue = financialTotals?.TotalRevenue ?? 0;
        var totalPartCost = financialTotals?.TotalPartsCost ?? 0;
        var totalLaborCost = financialTotals?.TotalLaborCost ?? 0;
        var totalTax = financialTotals?.TotalTax ?? 0;

        var netProfit = totalRevenue - totalPartCost - totalLaborCost - totalTax;

        var uniqueVehicles = await query.Select(s => s.VehicleId).Distinct().CountAsync(ct);
        var uniqueCustomers = await query
            .Select(s => s.Vehicle!.CustomerId)
            .Distinct()
            .CountAsync(ct);

        var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;
        var laborCostRatio = totalRevenue > 0 ? (totalLaborCost / totalRevenue) * 100 : 0;
        var partsCostRatio = totalRevenue > 0 ? (totalPartCost / totalRevenue) * 100 : 0;

        var completionRate =
            totalWorkOrders > 0 ? ((decimal)stats!.Completed / totalWorkOrders) * 100 : 0;
        var cancellationRate =
            totalWorkOrders > 0 ? ((decimal)stats!.Cancelled / totalWorkOrders) * 100 : 0;
        var averageRevenuePerOrder = totalWorkOrders > 0 ? (totalRevenue / totalWorkOrders) : 0;
        var ordersPerVehicle = uniqueVehicles > 0 ? ((decimal)totalWorkOrders / uniqueVehicles) : 0;

        return new TodayWorkOrderStatsDto
        {
            Date = request.Date,
            Total = totalWorkOrders,
            Scheduled = stats!.Scheduled,
            InProgress = stats.InProgress,
            Completed = stats.Completed,
            Cancelled = stats.Cancelled,
            TotalRevenue = totalRevenue,
            TotalPartsCost = totalPartCost,
            TotalLaborCost = totalLaborCost,
            UniqueVehicles = uniqueVehicles,
            UniqueCustomers = uniqueCustomers,
            NetProfit = netProfit,
            ProfitMargin = profitMargin,
            CompletionRate = completionRate,
            AverageRevenuePerOrder = averageRevenuePerOrder,
            OrdersPerVehicle = ordersPerVehicle,
            PartsCostRatio = partsCostRatio,
            LaborCostRatio = laborCostRatio,
            CancellationRate = cancellationRate,
        };
    }
}
