namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A lista de fases ou a lista de etapas que a leitura recusou leva as bancas dela: o que a regra exige dessas bancas não se
/// sabe, e o item do painel fica pendente em vez de aprovado por omissão. Sem recusa, o mesmo documento tem o item aprovado.
/// </summary>
public sealed class SuspensaoDasBancasPorFaseOuEtapaRecusadaTests
{
    private static readonly CorretoresDasEtapas NenhumLido = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    private static bool ItemAprovado(string codigo, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(cronograma, etapas, NenhumLido).Single(item => item.Codigo == codigo).Ok;

    [Theory(DisplayName = "A lista de fases recusada na leitura deixa pendente o item das bancas, e sem recusa o item é aprovado")]
    [InlineData("cronograma_banca_com_regra_de_deliberacao")]
    [InlineData("cronograma_recurso_com_banca_que_julga")]
    [InlineData("cronograma_tipo_de_banca_com_o_minimo_de_bancas")]
    public void ListaDeFasesRecusada_ItemFicaPendente(string item)
    {
        EtapasDoProcesso semEtapas = EtapasDoProcesso.Das([]);

        ItemAprovado(item, CronogramaDoProcesso.Do([]), semEtapas).Should().BeTrue();
        ItemAprovado(item, CronogramaDoProcesso.Do([], ComPartesRecusadas("fases")), semEtapas).Should().BeFalse();
    }

    [Theory(DisplayName = "A lista de etapas recusada na leitura deixa pendente o item das bancas, e sem recusa o item é aprovado")]
    [InlineData("cronograma_banca_com_regra_de_deliberacao")]
    [InlineData("cronograma_recurso_com_banca_que_julga")]
    [InlineData("cronograma_tipo_de_banca_com_o_minimo_de_bancas")]
    public void ListaDeEtapasRecusada_ItemFicaPendente(string item)
    {
        CronogramaDoProcesso semFases = CronogramaDoProcesso.Do([]);

        ItemAprovado(item, semFases, EtapasDoProcesso.Das([])).Should().BeTrue();
        ItemAprovado(item, semFases, EtapasDoProcesso.Das([], ComPartesRecusadas("etapas"))).Should().BeFalse();
    }
}
