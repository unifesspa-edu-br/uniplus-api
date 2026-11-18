namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Identificador da situação acadêmica no SIGAA, sempre positivo.</summary>
public sealed record IdDaSituacaoNoSigaa
{
    public int Valor { get; }

    private IdDaSituacaoNoSigaa(int valor) => Valor = valor;

    public static Result<IdDaSituacaoNoSigaa> Criar(int valor) =>
        valor > 0
            ? Result<IdDaSituacaoNoSigaa>.Success(new IdDaSituacaoNoSigaa(valor))
            : Result<IdDaSituacaoNoSigaa>.Failure(new DomainError(
                DiscentesErrorCodes.SituacaoAcademica.IdInvalido, "Id da situação deve ser positivo."));
}
