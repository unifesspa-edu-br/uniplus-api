namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A definição única de início e de conclusão de fase para a retificação (UNI-REQ-0211, UNI-REQ-0333), medida
/// sobre o cronograma da versão publicada vigente e nunca sobre o documento novo. A fase começou quando
/// <see cref="FasesIniciadas"/> a traz. A fase que declara definitivo, na própria fase ou na fase concluinte, está
/// concluída quando todos os definitivos dela foram publicados; a que não declara definitivo está concluída
/// quando a fase seguinte, a de menor momento maior que o dela, já começou. A fase sem seguinte e sem definitivo
/// não conclui. O momento da fase vem de <see cref="MomentoDaFase"/>, de modo que a janela de isenção ordenada
/// antes da inscrição não conclui a inscrição.
/// </summary>
public sealed record AndamentoDasFases(
    IReadOnlyCollection<FaseCronograma> FasesDaBase, IReadOnlyCollection<EtapaProcesso> EtapasDaBase, FatosDaExecucao Fatos)
{
    public AndamentoDaFase Da(FaseCronograma faseDaBase)
    {
        ArgumentNullException.ThrowIfNull(faseDaBase);

        if (!Fatos.FasesIniciadas.Contem(faseDaBase.FaseCanonicaOrigemId))
        {
            return AndamentoDaFase.PorComecar;
        }

        return EstaConcluida(faseDaBase) ? AndamentoDaFase.Concluida : AndamentoDaFase.EmAndamento;
    }

    private bool EstaConcluida(FaseCronograma fase)
    {
        if (DefinitivosQueConcluem(fase) is not { Count: > 0 } definitivos)
        {
            return AFaseSeguinteComecou(fase);
        }

        Dictionary<Guid, IdentidadeDoItemDoEdital> identidades = IdentidadeDoItemDoEdital.Dos(FasesDaBase, EtapasDaBase);

        return definitivos.All(definitivo => Fatos.ItensPublicados.Contains(identidades[definitivo]));
    }

    private HashSet<Guid> DefinitivosQueConcluem(FaseCronograma fase)
    {
        IEnumerable<Guid> proprios = fase.Produtos.Where(static p => p.Papel == PapelProdutoFase.Definitivo).Select(static p => p.Id);
        IEnumerable<Guid> dosPreliminares = fase.Produtos
            .Where(static p => p.Papel == PapelProdutoFase.Preliminar)
            .SelectMany(p => PreliminaresDaMateria.DefinitivosQueFecham(FasesDaBase, EtapasDaBase, p.Id));

        return [.. proprios, .. dosPreliminares];
    }

    private bool AFaseSeguinteComecou(FaseCronograma fase)
    {
        int momento = MomentoDaFase.Da(fase, FasesDaBase);
        int[] seguintes = [.. FasesDaBase.Select(f => MomentoDaFase.Da(f, FasesDaBase)).Where(outro => outro > momento)];

        return seguintes.Length > 0
            && FasesDaBase.Any(f => MomentoDaFase.Da(f, FasesDaBase) == seguintes.Min() && Fatos.FasesIniciadas.Contem(f.FaseCanonicaOrigemId));
    }
}
