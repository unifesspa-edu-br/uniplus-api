namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Identificador da unidade acadêmica do curso no SIGAA, sempre positivo.</summary>
public sealed record IdDaUnidadeNoSigaa
{
    public int Valor { get; }

    private IdDaUnidadeNoSigaa(int valor) => Valor = valor;

    public static Result<IdDaUnidadeNoSigaa> Criar(int valor) =>
        valor > 0
            ? Result<IdDaUnidadeNoSigaa>.Success(new IdDaUnidadeNoSigaa(valor))
            : Result<IdDaUnidadeNoSigaa>.Failure(new DomainError(
                DiscentesErrorCodes.Curso.UnidadeIdInvalido, "Id da unidade deve ser positivo."));
}
