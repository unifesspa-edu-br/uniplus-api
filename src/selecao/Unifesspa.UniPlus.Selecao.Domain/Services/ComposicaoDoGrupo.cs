namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Coerência estrutural entre os descendentes de um grupo de exigências.</summary>
internal static class ComposicaoDoGrupo
{
    internal static DomainError? Recusa(IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade) =>
        Recusas(filhos, repetePorEntidade).FirstOrDefault()?.Error;

    internal static List<FieldError> Recusas(IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade)
    {
        if (RecusaSeVazio(filhos.Count) is { } vazio)
        {
            return [new("filhos", vazio)];
        }
        if (ContemCiclo(filhos))
        {
            return [new("filhos", new DomainError("NoExigencia.ArvoreComCiclo",
                "Este grupo contém uma repetição circular de itens. Organize os grupos sem repetir um item dentro de si."))];
        }
        return [.. RecusasDasFasesEFinalidades(filhos), .. RecusaDaRepeticao(filhos, repetePorEntidade)];
    }

    internal static DomainError? RecusaSeVazio(int quantidadeDeFilhos) => quantidadeDeFilhos == 0
        ? new("NoExigencia.GrupoVazio", "Este grupo não contém itens. Acrescente ao menos um item ao grupo.") : null;

    private static IEnumerable<FieldError> RecusasDasFasesEFinalidades(IReadOnlyList<NoExigencia> filhos)
    {
        if (FaseComumDosFilhos(filhos) is null)
        {
            yield return new("filhos", new DomainError("NoExigencia.GrupoComFasesDiferentes",
                "Os itens deste grupo pertencem a fases diferentes. Escolha a mesma fase para todos os itens do grupo."));
        }
        if (!FinalidadeComumDosFilhos(filhos, out _))
        {
            yield return new("filhos", new DomainError("NoExigencia.GrupoComFinalidadesDiferentes",
                "Os documentos deste grupo pertencem a formulários diferentes. Escolha o mesmo formulário para todos os documentos do grupo."));
        }
    }

    private static IEnumerable<FieldError> RecusaDaRepeticao(IReadOnlyList<NoExigencia> filhos, string? repeticao) =>
        repeticao is not null && filhos.Any(ContemRepeticaoDeEntidade)
            ? [new("repetePorEntidade", new DomainError("NoExigencia.RepeticaoDeEntidadeAninhada",
                "Este grupo repete itens que já se repetem por outro grupo. Retire uma das repetições."))] : [];

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
