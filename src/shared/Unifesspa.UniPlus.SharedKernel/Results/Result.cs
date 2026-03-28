namespace Unifesspa.UniPlus.SharedKernel.Results;

public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError? Error { get; }

    private Result(bool isSuccess, DomainError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(DomainError error) => new(false, error);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public DomainError? Error { get; }

    private Result(bool isSuccess, T? value, DomainError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

#pragma warning disable CA1000 // Factory methods em tipos genéricos são padrão Result<T>
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(DomainError error) => new(false, default, error);
#pragma warning restore CA1000

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<DomainError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}
