namespace Unifesspa.UniPlus.Kernel.Results;

public sealed class Result
{
    private static readonly IReadOnlyList<FieldError> Nenhum = [];

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Todas as violações do lote, na ordem em que as regras foram avaliadas.
    /// Vazio em sucesso. <see cref="Error"/> é sempre a primeira — mantido para
    /// os callers mono-erro existentes (ADR-0125).
    /// </summary>
    public IReadOnlyList<FieldError> Errors { get; }

    public DomainError? Error => Errors.Count > 0 ? Errors[0].Error : null;

    private Result(bool isSuccess, IReadOnlyList<FieldError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static Result Success() => new(true, Nenhum);

    public static Result Failure(DomainError error) => new(false, [new(null, error)]);

    /// <summary>
    /// Falha com mais de uma violação (ex.: `Entidade.ValidarCampos` acumulando
    /// regras em vez de retornar na primeira). Nome distinto de <see cref="Failure(DomainError)"/>
    /// de propósito: um overload `Failure(IReadOnlyList&lt;FieldError&gt;)` seria
    /// ambíguo para uma chamada com `null!` (ambos os overloads aceitariam),
    /// quebrando o comportamento hoje fixado em <c>ResultTests.Failure_ComErroNulo_NaoLancaEDeixaErrorNulo</c>.
    /// </summary>
    public static Result ValidationFailure(IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        // Cópia defensiva: um List<FieldError> mutável do caller não pode continuar
        // mudando Errors por baixo do Result depois de construído (ex.: caller que
        // reusa a lista e a esvazia em seguida deixaria Errors[0] estourar).
        FieldError[] copia = [.. errors];
        if (copia.Length == 0)
        {
            throw new ArgumentException("ValidationFailure exige ao menos um FieldError.", nameof(errors));
        }

        return new(false, copia);
    }

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
    private static readonly IReadOnlyList<FieldError> Nenhum = [];

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }

    /// <summary>Ver <see cref="Result.Errors"/>.</summary>
    public IReadOnlyList<FieldError> Errors { get; }

    public DomainError? Error => Errors.Count > 0 ? Errors[0].Error : null;

    private Result(bool isSuccess, T? value, IReadOnlyList<FieldError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, Nenhum);

    public static Result<T> Failure(DomainError error) => new(false, default, [new(null, error)]);

    /// <summary>Ver <see cref="Result.ValidationFailure"/> — mesmo motivo do nome distinto.</summary>
    public static Result<T> ValidationFailure(IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        // Ver comentário equivalente em Result.ValidationFailure — cópia defensiva.
        FieldError[] copia = [.. errors];
        if (copia.Length == 0)
        {
            throw new ArgumentException("ValidationFailure exige ao menos um FieldError.", nameof(errors));
        }

        return new(false, default, copia);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<DomainError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}
