using MechanicShop.Application.Common.Interfaces;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class LoggingBehavior<TRequest>(ILogger<TRequest> Logger, IUser User)
    : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger = Logger;
    private readonly IUser _user = User;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        var userId = _user.Id ?? "Anonymous";

        _logger.LogInformation(
            "MechanicShop Request: {Name} | UserId: {@UserId} | Payload: {@Request}",
            requestName,
            userId,
            request
        );

        return Task.CompletedTask;
    }
}
