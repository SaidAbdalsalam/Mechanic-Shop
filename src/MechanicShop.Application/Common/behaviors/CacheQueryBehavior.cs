using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results.Abstraction;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Common.behaviors;

public sealed class CacheQueryBehavior<TRequest, TResponse>(
    HybridCache Cache,
    ILogger<CacheQueryBehavior<TRequest, TResponse>> Logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly HybridCache _cache = Cache;
    private readonly ILogger<CacheQueryBehavior<TRequest, TResponse>> _logger = Logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        if (request is not ICachedQuery cacheRequest)
        {
            return await next(ct);
        }
        _logger.LogInformation("Checking cache for {RequestName}", typeof(TRequest).Name);

        return await _cache.GetOrCreateAsync(
            cacheRequest.CacheKey,
            async factory => await next(ct),
            options: new HybridCacheEntryOptions { Expiration = cacheRequest.Expiration },
            tags: cacheRequest.Tags,
            cancellationToken: ct
        );
    }
}
