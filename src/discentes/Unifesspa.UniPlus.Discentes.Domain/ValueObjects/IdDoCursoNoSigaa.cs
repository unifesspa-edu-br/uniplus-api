namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Identificador do curso no SIGAA, sempre positivo.</summary>
public sealed record IdDoCursoNoSigaa
{
    public int Valor { get; }

    private IdDoCursoNoSigaa(int valor) => Valor = valor;

    public static Result<IdDoCursoNoSigaa> Criar(int valor) =>
        valor > 0
            ? Result<IdDoCursoNoSigaa>.Success(new IdDoCursoNoSigaa(valor))
            : Result<IdDoCursoNoSigaa>.Failure(new DomainError(
                DiscentesErrorCodes.Curso.IdInvalido, "Id do curso deve ser positivo."));
}
