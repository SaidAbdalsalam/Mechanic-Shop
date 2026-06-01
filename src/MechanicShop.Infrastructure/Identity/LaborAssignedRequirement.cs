using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Identity;

public sealed class LaborAssignedRequirement : IAuthorizationRequirement;

public sealed class LaborAssignedHandler(
    IAppDbContext context,
    IHttpContextAccessor httpContextAccessor
) : AuthorizationHandler<LaborAssignedRequirement>
{
    private readonly IAppDbContext _context = context;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LaborAssignedRequirement requirement
    )
    {
        if (context.User.IsInRole(nameof(Role.Manager)))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || string.IsNullOrEmpty(userIdClaim.Value))
        {
            context.Fail();
            return;
        }

        var userId = userIdClaim.Value;

        var httpContext = _httpContextAccessor.HttpContext;
        if (
            httpContext is null
            || !httpContext.Request.RouteValues.TryGetValue("workOrderId", out var routeValue)
            || routeValue is null
        )
        {
            context.Fail();
            return;
        }

        var workOrderString = routeValue.ToString();

        if (!Guid.TryParse(workOrderString, out var workOrderId))
        {
            context.Fail();
            return;
        }

        var isAssigned = await _context.WorkOrders.AnyAsync(wo =>
            wo.Id == workOrderId && wo.LaborId == Guid.Parse(userId)
        );

        if (isAssigned)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }
}
