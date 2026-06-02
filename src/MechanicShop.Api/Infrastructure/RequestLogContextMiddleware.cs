using Serilog.Context;

namespace MechanicShop.Api.Infrastructure;

public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
        {
            return _next(context);
        }
    }
}
