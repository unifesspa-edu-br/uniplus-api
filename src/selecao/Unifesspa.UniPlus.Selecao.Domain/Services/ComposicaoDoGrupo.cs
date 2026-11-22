namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Coerência estrutural entre os descendentes de um grupo de exigências.</summary>
internal static class ComposicaoDoGrupo
{
    internal static DomainError? Recusa(IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao) =>
        Recusas(itens, grupoDoFormularioParaRepeticao).FirstOrDefault()?.Error;

    internal static List<FieldError> Recusas(IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao)
    {
        if (RecusaSeVazio(itens.Count) is { } vazio)
        {
            return [new("itens", vazio)];
        }
        if (ContemCiclo(itens))
        {
            return [new("itens", new DomainError("RegraDeComprovacao.RegraContemASiMesma",
                "Este grupo contém uma repetição circular de itens. Organize os grupos sem repetir um item dentro de si."))];
        }
        return [.. RecusasDasFasesEFinalidades(itens), .. RecusaDaRepeticao(itens, grupoDoFormularioParaRepeticao)];
    }

    internal static DomainError? RecusaSeVazio(int quantidadeDeItens) => quantidadeDeItens == 0
        ? new("RegraDeComprovacao.GrupoSemItens", "Este grupo não contém itens. Acrescente ao menos um item ao grupo.") : null;

    private static IEnumerable<FieldError> RecusasDasFasesEFinalidades(IReadOnlyList<RegraDeComprovacao> itens)
    {
        if (FaseComumDosItens(itens) is null)
        {
            yield return new("itens", new DomainError("RegraDeComprovacao.GrupoComFasesDiferentes",
                "Os itens deste grupo pertencem a fases diferentes. Escolha a mesma fase para todos os itens do grupo."));
        }
        if (!FinalidadeComumDosItens(itens, out _))
        {
            yield return new("itens", new DomainError("RegraDeComprovacao.GrupoComFormulariosDiferentes",
                "Os documentos deste grupo pertencem a formulários diferentes. Escolha o mesmo formulário para todos os documentos do grupo."));
        }
    }

    private static IEnumerable<FieldError> RecusaDaRepeticao(IReadOnlyList<RegraDeComprovacao> itens, string? repeticao) =>
        repeticao is not null && itens.Any(ContemRepeticaoPorGrupo)
            ? [new("grupoDoFormularioParaRepeticao", new DomainError("RegraDeComprovacao.RepeticaoDentroDeRepeticao",
                "Este grupo repete itens que já se repetem por outro grupo. Retire uma das repetições."))] : [];

    private static bool ContemRepeticaoPorGrupo(RegraDeComprovacao regra) =>
        regra.GrupoDoFormularioParaRepeticao is not null || regra.Itens.Any(ContemRepeticaoPorGrupo);

    internal static Guid? FaseComumDosItens(IReadOnlyList<RegraDeComprovacao> itens)
    {
        HashSet<Guid> fases = [];
        foreach (Guid? fase in itens.Select(static item => item.FaseComum()))
        {
            if (fase is null)
            {
                return null;
            }

            fases.Add(fase.Value);
        }

        return fases.Count == 1 ? fases.First() : null;
    }

    internal static bool FinalidadeComumDosItens(IReadOnlyList<RegraDeComprovacao> itens, out FinalidadeFormulario? finalidade)
    {
        finalidade = null;
        if (itens.Count == 0)
        {
            return true;
        }
        if (!itens[0].TentarFinalidadeComum(out finalidade))
        {
            return false;
        }
        FinalidadeFormulario? comum = finalidade;
        return itens.Skip(1).All(item => item.TentarFinalidadeComum(out FinalidadeFormulario? doItem) && doItem == comum);
    }

    internal static bool ContemCiclo(IReadOnlyList<RegraDeComprovacao> regras)
    {
        HashSet<RegraDeComprovacao> visitados = new(ReferenceEqualityComparer.Instance);
        return regras.Any(regra => !Visitar(regra, visitados));
    }

    private static bool Visitar(RegraDeComprovacao regra, HashSet<RegraDeComprovacao> visitados) =>
        visitados.Add(regra) && regra.Itens.All(item => Visitar(item, visitados));
}
