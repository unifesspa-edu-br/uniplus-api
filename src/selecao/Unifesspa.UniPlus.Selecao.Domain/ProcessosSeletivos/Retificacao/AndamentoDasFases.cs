namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A definição única de início e de conclusão de fase para a retificação (UNI-REQ-0211, UNI-REQ-0333), medida
/// sobre o cronograma da versão publicada vigente e nunca sobre o documento novo. A fase começou quando
/// <see cref="FasesIniciadas"/> a traz. A fase que declara definitivo está concluída quando todos os definitivos
/// dela foram publicados nela mesma; a que não declara definitivo está concluída quando o período dela termina na
/// versão vigente, inclusive o novo fim de uma prorrogação, e só se ela começou. A fase que começou e passou do fim
/// volta a correr quando uma versão nova prorroga o fim. No instante exato do fim o período ainda corre.
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

    private bool EstaConcluida(FaseCronograma fase) =>
        fase.PublicaResultadoDefinitivo ? TodosOsDefinitivosPublicados(fase) : PeriodoEncerrado(fase);

    private bool PeriodoEncerrado(FaseCronograma fase) =>
        new JanelaDeTempo(fase.Inicio, fase.Fim).PosicaoDe(Fatos.Instante) == PosicaoNaJanela.Depois;

    private bool TodosOsDefinitivosPublicados(FaseCronograma fase)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> identidades = IdentidadeDoItemDoEdital.Dos(FasesDaBase, EtapasDaBase);

        return fase.Produtos
            .Where(static produto => produto.Papel == PapelProdutoFase.Definitivo)
            .All(definitivo => Fatos.ItensPublicados.Contains(identidades[definitivo.Id]));
    }
}
