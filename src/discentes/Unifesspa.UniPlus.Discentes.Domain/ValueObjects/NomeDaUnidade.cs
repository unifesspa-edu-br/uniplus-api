namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Nome da unidade acadêmica do curso como o SIGAA o entrega: obrigatório e dentro do que a
/// réplica comporta.
/// </summary>
public sealed record NomeDaUnidade
{
    public string Valor { get; }

    private NomeDaUnidade(string valor) => Valor = valor;

    public static Result<NomeDaUnidade> Criar(string? texto)
    {
        DomainError? erro = RecusarSeVazio(texto)
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.NomeDaUnidade,
                DiscentesErrorCodes.Curso.UnidadeNomeLongo,
                "Nome da unidade excede o que a réplica comporta.");

        return erro is null
            ? Result<NomeDaUnidade>.Success(new NomeDaUnidade(texto!))
            : Result<NomeDaUnidade>.Failure(erro);
    }

    internal static DomainError? RecusarSeVazio(string? texto) =>
        TextoDaReplica.RecusarSeVazio(texto, DiscentesErrorCodes.Curso.UnidadeNomeVazio, "Nome da unidade é obrigatório.");
}
