namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A unidade responsável que a leitura do documento recusou não é unidade ausente: a causa é a da leitura, a regra não a repete
/// na mesma fase, e o item do painel fica pendente em vez de aprovado. A fase sem unidade que a leitura não recusou segue recusada.
/// </summary>
public sealed class SuspensaoPorUnidadeRecusadaDaFaseTests
{
    private const string CodigoDaRecusa = "ProcessoSeletivo.FaseSemUnidadeResponsavel";
    private const string ItemDaUnidade = "cronograma_fase_sem_unidade_responsavel";

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static FaseCronograma FaseSemUnidade(string codigo, int ordem) =>
        new ModeloDeFase(codigo, ordem) { ComUnidade = false }.Janela(ordem, ordem + 1).Adotada();

    private static string?[] CamposDasRecusas(CronogramaDoProcesso cronograma) =>
        [.. cronograma.ConferirCompletude(Utc).Where(static recusa => recusa.Error.Code == CodigoDaRecusa).Select(static recusa => recusa.Field)];

    private static bool ItemAprovado(CronogramaDoProcesso cronograma) =>
        cronograma.ItensDaCompletude(Utc).Single(static item => item.Codigo == ItemDaUnidade).Ok;

    [Fact(DisplayName = "A fase sem unidade responsável é recusada quando a leitura não recusou a unidade")]
    public void FaseSemUnidade_SemParteRecusada_Recusa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseSemUnidade("A", 1)]);

        CamposDasRecusas(cronograma).Should().Equal("fases[0].unidadeResponsavel");
        ItemAprovado(cronograma).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase cuja unidade foi recusada pela leitura não é recusada como sem unidade, e a outra fase sem unidade segue recusada")]
    public void FaseSemUnidade_ComUnidadeRecusada_SuspendeSoNaFaseDaParte()
    {
        FaseCronograma[] fases = [FaseSemUnidade("A", 1), FaseSemUnidade("B", 2)];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[0].unidadeResponsavel"));

        CamposDasRecusas(cronograma).Should().Equal("fases[1].unidadeResponsavel");
        ItemAprovado(cronograma).Should().BeFalse();
    }

    [Fact(DisplayName = "O item da unidade responsável fica pendente, e não aprovado, quando a única unidade sem valor foi recusada pela leitura")]
    public void ItemDaUnidade_ComUnidadeRecusada_FicaPendente()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseSemUnidade("A", 1)], ComPartesRecusadas("fases[0].unidadeResponsavel"));

        CamposDasRecusas(cronograma).Should().BeEmpty();
        ItemAprovado(cronograma).Should().BeFalse();
    }

    [Fact(DisplayName = "A parte recusada em outro campo da fase não suspende a recusa da unidade ausente")]
    public void FaseSemUnidade_ComOutraParteRecusada_ContinuaRecusada()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseSemUnidade("A", 1)], ComPartesRecusadas("fases[0].produtos"));

        CamposDasRecusas(cronograma).Should().Equal("fases[0].unidadeResponsavel");
    }

    [Fact(DisplayName = "A unidade recusada na fase de outra posição não suspende a recusa da fase sem unidade")]
    public void FaseSemUnidade_ComUnidadeRecusadaEmOutraPosicao_ContinuaRecusada()
    {
        FaseCronograma[] fases = [FaseSemUnidade("A", 1), new ModeloDeFase("B", 2).Janela(2, 3).Adotada()];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[1].unidadeResponsavel"));

        CamposDasRecusas(cronograma).Should().Equal("fases[0].unidadeResponsavel");
    }
}
