namespace Unifesspa.UniPlus.Kernel.Results;

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

    /// <summary>
    /// <see langword="true"/> quando o resultado falhou com exatamente este
    /// código de erro. Permite que callers (ex.: controllers) ramifiquem por
    /// causa específica sem depender diretamente do tipo <see cref="DomainError"/>
    /// (ADR-0024 — mapeamento de erro é responsabilidade de <c>IDomainErrorMapper</c>;
    /// este método só expõe o código para decisão de fluxo, nunca o objeto).
    /// </summary>
    public bool HasErrorCode(string code) => Error?.Code == code;
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

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(DomainError error) => new(false, default, error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<DomainError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}
