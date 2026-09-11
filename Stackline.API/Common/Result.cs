namespace Stackline.API.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type);

public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "A failed result has no value.");

    public Error Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException(
            "A successful result has no error.");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);
}