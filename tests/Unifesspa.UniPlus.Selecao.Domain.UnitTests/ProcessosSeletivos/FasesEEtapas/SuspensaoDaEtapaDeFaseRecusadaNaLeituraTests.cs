namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A etapa que abre recurso e cita uma fase que a leitura do documento recusou inteira pode ser julgada pela banca que a fase
/// declarou: o painel fica pendente e a conferência não manda a pessoa declarar uma banca que ela já declarou. A etapa cuja fase
/// foi aceita, ou que não cita fase recusada, segue conferida.
/// </summary>
public sealed class SuspensaoDaEtapaDeFaseRecusadaNaLeituraTests
{
    private const string ItemDoRecurso = "cronograma_recurso_com_banca_que_julga";
    private const string RecursoSemBanca = "Cronograma.RecursoSemBanca";

    private static readonly ReferenciaRegra AvaliacaoUnica =
        ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", new string('a', 64));

    private static readonly CorretoresDasEtapas NenhumLido = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    private static EtapasDoProcesso EtapaDaFase(string codigoDaFase) =>
        EtapasDoProcesso.Das([(new ModeloDeEtapa("Redação") with
        {
            FaseCodigo = codigoDaFase,
            Forma = FormaDeAvaliacao.Humana,
            RegraConsolidacao = AvaliacaoUnica,
            Passo = 0.5m,
            EmiteParecerIndividual = true,
            Recursos = [RecursoModelo.NaCiencia()],
        }).Adotada()]);

    private static FaseCronograma FaseSemRecurso(string codigo) => new ModeloDeFase(codigo, 1).Janela(1, 5).Adotada();

    private static (string? Campo, string Codigo)[] Causas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        [.. QuemJulgaORecurso.SemBanca(cronograma, etapas).Select(static causa => (causa.Field, causa.Error.Code))];

    private static bool ItemAprovado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, etapas, NenhumLido).Single(static item => item.Codigo == ItemDoRecurso).Ok;

    [Fact(DisplayName = "A etapa que cita a fase recusada inteira não recebe a recusa de recurso sem banca, e o item do painel fica pendente")]
    public void EtapaDaFaseRecusada_NaoRecebeARecusa_EItemFicaPendente()
    {
        EtapasDoProcesso etapas = EtapaDaFase("RESULTADO");

        Causas(CronogramaDoProcesso.Do([]), etapas).Should().Equal(("etapas[0].bancas", RecursoSemBanca));
        Causas(CronogramaDoProcesso.Do([], ComPartesRecusadas("fases")), etapas).Should().BeEmpty();
        ItemAprovado(CronogramaDoProcesso.Do([], ComPartesRecusadas("fases")), etapas).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa cuja fase foi aceita segue recusada quando outra fase foi recusada na leitura")]
    public void EtapaDeFaseAceita_SegueRecusada_AoLadoDeFaseRecusada()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseSemRecurso("ANALISE")], ComPartesRecusadas("fases"));

        Causas(cronograma, EtapaDaFase("ANALISE")).Should().Equal(("etapas[0].bancas", RecursoSemBanca));
    }
}
