namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Nome do curso como o SIGAA o entrega: obrigatório e dentro do que a réplica comporta.</summary>
public sealed record NomeDoCurso
{
    public string Valor { get; }

    private NomeDoCurso(string valor) => Valor = valor;

    public static Result<NomeDoCurso> Criar(string? texto)
    {
        DomainError? erro = RecusarSeVazio(texto)
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.NomeDoCurso,
                DiscentesErrorCodes.Curso.NomeLongo,
                "Nome do curso excede o que a réplica comporta.");

        return erro is null
            ? Result<NomeDoCurso>.Success(new NomeDoCurso(texto!))
            : Result<NomeDoCurso>.Failure(erro);
    }

    internal static DomainError? RecusarSeVazio(string? texto) =>
        TextoDaReplica.RecusarSeVazio(texto, DiscentesErrorCodes.Curso.NomeVazio, "Nome do curso é obrigatório.");
}
