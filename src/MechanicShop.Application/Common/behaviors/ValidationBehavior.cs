using FluentValidation;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Common.Results.Abstraction;
using MediatR;

namespace MechanicShop.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct
    )
    {
        if (!validators.Any())
        {
            return await next(ct);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));

        var errors = results
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .Select(failure => Error.Validation(failure.ErrorCode, failure.ErrorMessage))
            .Distinct()
            .ToList();

        if (errors.Count > 0)
        {
            return (dynamic)errors;
        }

        return await next(ct);
    }
}
