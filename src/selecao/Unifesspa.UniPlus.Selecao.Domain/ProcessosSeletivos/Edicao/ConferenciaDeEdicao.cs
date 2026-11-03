namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leva a conferência de uma edição à forma única do protocolo: o plano que a aplicação recebe. Quando a
/// edição não calcula nada além do valor recebido, o plano é o próprio valor.
/// </summary>
internal static class ConferenciaDeEdicao
{
    internal static Result<T> Com<T>(this DomainError? recusa, T valor) =>
        recusa is null ? Result<T>.Success(valor) : Result<T>.Failure(recusa);

    internal static Result<T> Com<T>(this Result conferencia, T valor)
    {
        ArgumentNullException.ThrowIfNull(conferencia);

        return conferencia.IsSuccess ? Result<T>.Success(valor) : Result<T>.ValidationFailure(conferencia.Errors);
    }

    /// <summary>Edição sem regra de conferência além da guarda.</summary>
    internal static Result<T> Livre<T>(T valor) => Result<T>.Success(valor);
}
