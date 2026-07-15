namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Conjunção (E) de <see cref="CondicaoDnf"/> — uma cláusula da forma normal
/// disjuntiva de <see cref="PredicadoDnf"/> (ADR-0111, Story #847). Uma
/// cláusula vazia nunca é um objeto construível: a factory rejeita a lista
/// vazia ou nula.
/// </summary>
public sealed record ClausulaDnf
{
    private ClausulaDnf(IReadOnlyList<CondicaoDnf> condicoes)
    {
        Condicoes = condicoes;
    }

    public IReadOnlyList<CondicaoDnf> Condicoes { get; }

    public static Result<ClausulaDnf> Criar(IReadOnlyList<CondicaoDnf>? condicoes)
    {
        if (condicoes is not { Count: > 0 })
        {
            return Result<ClausulaDnf>.Failure(new DomainError(
                "ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
        }

        return Result<ClausulaDnf>.Success(new ClausulaDnf([.. condicoes]));
    }
}
