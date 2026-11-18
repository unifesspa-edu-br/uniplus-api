namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Identificador do vínculo do discente no SIGAA, sempre positivo.</summary>
public sealed record IdDoDiscenteNoSigaa
{
    public long Valor { get; }

    private IdDoDiscenteNoSigaa(long valor) => Valor = valor;

    public static Result<IdDoDiscenteNoSigaa> Criar(long valor) =>
        valor > 0
            ? Result<IdDoDiscenteNoSigaa>.Success(new IdDoDiscenteNoSigaa(valor))
            : Result<IdDoDiscenteNoSigaa>.Failure(new DomainError(
                DiscentesErrorCodes.VinculoDiscente.IdSigaaInvalido, "Id SIGAA deve ser positivo."));
}
