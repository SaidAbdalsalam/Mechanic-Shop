using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public sealed class AssignLaborCommandHandler(
    IAppDbContext Context,
    ILogger<AssignLaborCommandHandler> Logger,
    HybridCache Cache,
    IWorkOrderPolicy WorkOrderPolicy
) : IRequestHandler<AssignLaborCommand, Result<Updated>>
{
    private readonly IAppDbContext _context = Context;
    private readonly ILogger<AssignLaborCommandHandler> _logger = Logger;
    private readonly HybridCache _cache = Cache;
    private readonly IWorkOrderPolicy _workOrderPolicy = WorkOrderPolicy;

    public async Task<Result<Updated>> Handle(AssignLaborCommand command, CancellationToken ct)
    {
        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(
            wo => wo.Id == command.WorkOrderId,
            ct
        );
        if (workOrder is null)
        {
            _logger.LogWarning("Work order with id: {WorkOrderId} not found", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var laborIsExist = await _context.Employees.AnyAsync(
            e => e.Id == command.LaborId && e.Role == Role.Labor,
            ct
        );

        if (!laborIsExist)
        {
            _logger.LogWarning("Labor with id: {LaborId} not found", command.LaborId);
            return ApplicationErrors.LaborNotFound;
        }
        var isLaborOccupied = await _workOrderPolicy.IsLaborOccupied(
            command.LaborId,
            workOrder.StartAtUtc,
            workOrder.EndAtUtc,
            command.WorkOrderId
        );
        if (isLaborOccupied)
            return ApplicationErrors.LaborOccupied;
        var updateLaborResult = workOrder.UpdateLabor(command.LaborId);

        if (updateLaborResult.IsError)
        {
            foreach (var error in updateLaborResult.Errors)
            {
                _logger.LogError(
                    "[LaborUpdate] {ErrorCode}: {ErrorDescription}",
                    error.Code,
                    error.Description
                );
            }
            return updateLaborResult.Errors;
        }

        await _context.SaveChangesAsync(ct);

        await _cache.RemoveByTagAsync("work-order", ct);
        return Result.Updated;
    }
}
