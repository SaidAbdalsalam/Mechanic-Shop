using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderStats;

public sealed record GetWorkOrderStatsQuery(DateOnly Date, TimeZoneInfo TimeZone)
    : IRequest<Result<TodayWorkOrderStatsDto>>;
