using System.ComponentModel;
using System.Text.Json.Serialization;
using MechanicShop.Domain.Common.Results.Abstraction;

namespace MechanicShop.Domain.Common.Results;

public readonly record struct Success;

public readonly record struct Updated;

public readonly record struct Deleted;

public readonly record struct Created;

public static class Result
{
    public static Success Success = default;
    public static Updated Updated = default;
    public static Deleted Deleted = default;
    public static Created Created = default;
}

public sealed class Result<TValue> : IResult<TValue>
{
    private readonly List<Error>? _errors = [];
    private readonly TValue? _value = default!;
    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;
    public List<Error> Errors => IsError ? _errors! : [];
    public TValue Value => IsSuccess ? _value! : default!;
    public Error TopError => (_errors!.Count > 0) ? _errors[0] : default;

    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("For serializer only.", true)]
    public Result(TValue? value, List<Error>? errors, bool isSuccess)
    {
        if (isSuccess)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _errors = [];
            IsSuccess = true;
        }
        else
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Provide at least one error.", nameof(errors));
            }

            _errors = errors;
            _value = default!;
            IsSuccess = false;
        }
    }

    private Result(Error error)
    {
        _errors = [error];
        IsSuccess = false;
    }

    private Result(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            throw new ArgumentException(
                "Cannot create an ErrorOr<TValue> from an empty collection of errors. Provide at least one error.",
                nameof(errors)
            );
        }

        _errors = errors;

        IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = value;

        IsSuccess = true;
    }

    public TValueNext Match<TValueNext>(
        Func<TValue, TValueNext> onValue,
        Func<List<Error>, TValueNext> onError
    ) => IsSuccess ? onValue(Value!) : onError(Errors);

    public static implicit operator Result<TValue>(Error error) => new(error);

    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

    public static implicit operator Result<TValue>(TValue value) => new(value);
}
