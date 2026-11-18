namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Coerência estrutural entre os descendentes de um grupo de exigências.</summary>
internal static class ComposicaoDoGrupo
{
    internal static DomainError? Recusa(IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade)
    {
        return RecusaDaTopologia(filhos) ?? RecusaDaFinalidadeERepeticao(filhos, repetePorEntidade);
    }

    private static DomainError? RecusaDaTopologia(IReadOnlyList<NoExigencia> filhos)
    {
        if (filhos.Count == 0)
        {
            return new("NoExigencia.GrupoVazio", "Um grupo E/OU não pode ter zero filhos.");
        }
        if (ContemCiclo(filhos))
        {
            return new("NoExigencia.ArvoreComCiclo", "A árvore de satisfação não pode conter ciclos.");
        }
        return FaseComumDosFilhos(filhos) is null
            ? new("NoExigencia.GrupoComFasesDiferentes", "Todos os nós de um grupo precisam pertencer à mesma fase do cronograma.") : null;
    }

    private static DomainError? RecusaDaFinalidadeERepeticao(IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade)
    {
        if (!FinalidadeComumDosFilhos(filhos, out _))
        {
            return new("NoExigencia.GrupoComFinalidadesDiferentes", "Todos os documentos de um grupo pertencem ao mesmo formulário.");
        }
        return repetePorEntidade is not null && filhos.Any(ContemRepeticaoDeEntidade)
            ? new("NoExigencia.RepeticaoDeEntidadeAninhada", "Uma subárvore repetida por entidade não pode conter outra.") : null;
    }

    private static bool ContemRepeticaoDeEntidade(NoExigencia no) =>
        no.RepetePorEntidade is not null || no.Filhos.Any(ContemRepeticaoDeEntidade);

    internal static Guid? FaseComumDosFilhos(IReadOnlyList<NoExigencia> filhos)
    {
        HashSet<Guid> fases = [];
        foreach (Guid? fase in filhos.Select(static filho => filho.FaseComum()))
        {
            if (fase is null)
            {
                return null;
            }

            fases.Add(fase.Value);
        }

        return fases.Count == 1 ? fases.First() : null;
    }

    internal static bool FinalidadeComumDosFilhos(IReadOnlyList<NoExigencia> filhos, out FinalidadeFormulario? finalidade)
    {
        finalidade = null;
        if (filhos.Count == 0)
        {
            return true;
        }
        if (!filhos[0].TentarFinalidadeComum(out finalidade))
        {
            return false;
        }
        FinalidadeFormulario? comum = finalidade;
        return filhos.Skip(1).All(filho => filho.TentarFinalidadeComum(out FinalidadeFormulario? doFilho) && doFilho == comum);
    }

    internal static bool ContemCiclo(IReadOnlyList<NoExigencia> raizes)
    {
        HashSet<NoExigencia> visitados = new(ReferenceEqualityComparer.Instance);
        return raizes.Any(raiz => !Visitar(raiz, visitados));
    }

    private static bool Visitar(NoExigencia no, HashSet<NoExigencia> visitados) =>
        visitados.Add(no) && no.Filhos.All(filho => Visitar(filho, visitados));
}
