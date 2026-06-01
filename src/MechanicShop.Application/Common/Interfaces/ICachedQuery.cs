using MechanicShop.Application.Common.Models;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Common.Interfaces;

public interface ICachedQuery
{
    public string CacheKey { get; }
    public string[] Tags { get; }
    public TimeSpan Expiration { get; }
}

public interface ICachedQuery<TResponse> : ICachedQuery, IRequest<TResponse>;
