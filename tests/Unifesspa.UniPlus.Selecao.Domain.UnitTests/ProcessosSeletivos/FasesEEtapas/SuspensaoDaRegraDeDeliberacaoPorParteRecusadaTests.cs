namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>
/// A banca que a leitura do documento recusou não chega às listas declaradas, e a regra de deliberação dela não se sabe: o
/// item do painel fica pendente em vez de aprovado por omissão, e as bancas aceitas seguem conferidas.
/// </summary>
public sealed class SuspensaoDaRegraDeDeliberacaoPorParteRecusadaTests
{
    private const string ItemDaDeliberacao = "cronograma_banca_com_regra_de_deliberacao";

    private static readonly DeclaracaoDaDeliberacao Valida =
        new(ModoDeDeliberacao.ColegiadaComVotacao, 3, 0, 2, CriterioDeVotacao.MaioriaSimples, DesempateDaVotacao.VotoDaPresidencia, EncerramentoDaVotacao.PresidenciaNoQuorum, null);

    private static readonly DeclaracaoDaDeliberacao Invalida =
        new(ModoDeDeliberacao.ColegiadaComVotacao, 0, 0, 0, CriterioDeVotacao.MaioriaSimples, null, EncerramentoDaVotacao.PresidenciaNoQuorum, null);

    private static readonly CorretoresDasEtapas NenhumLido = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    private static CronogramaDoProcesso CronogramaLido(params BancaNoDocumento[] bancas) =>
        Cronograma(Ler(Documento(Fase(0, "recurso", "RECURSO") with { Bancas = bancas })));

    private static ItemConformidade ItemDaDeliberacaoNoPainel(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(cronograma, etapas, NenhumLido).Single(static item => item.Codigo == ItemDaDeliberacao);

    [Fact(DisplayName = "A banca da fase recusada na leitura deixa pendente o item da regra de deliberação, e sem recusa o item é aprovado")]
    public void BancaDaFaseRecusadaNaLeitura_ItemFicaPendente()
    {
        EtapasDoProcesso semEtapas = EtapasDoProcesso.Das([]);
        CronogramaDoProcesso lidaInteira = CronogramaLido(Banca(0, "banca-recursos", "ANALISE_RECURSOS", [], Valida));
        CronogramaDoProcesso comBancaRecusada = CronogramaLido(Banca(0, "banca-recursos", "ANALISE_RECURSOS", [], Invalida));

        ItemDaDeliberacaoNoPainel(lidaInteira, semEtapas).Ok.Should().BeTrue();
        ItemDaDeliberacaoNoPainel(comBancaRecusada, semEtapas).Ok.Should().BeFalse();
        ConferenciaDasBancas.ConferirRegrasDeDeliberacao(comBancaRecusada, semEtapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca aceita sem regra de deliberação continua recusada, na posição dela entre as aceitas, quando a banca vizinha foi recusada na leitura")]
    public void BancaAceitaSemRegra_SegueRecusadaAoLadoDeBancaRecusada()
    {
        CronogramaDoProcesso cronograma = CronogramaLido(
            Banca(0, "banca-ruim", "ANALISE_RECURSOS", [], Invalida),
            Banca(1, "banca-sem-regra", "ANALISE_RECURSOS", []));

        Causas(ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, EtapasDoProcesso.Das([])))
            .Should().Equal(("fases[0].bancasRequeridas[0]", "Banca.RegraDeDeliberacaoObrigatoria"));
    }

    [Fact(DisplayName = "A banca da etapa com parte recusada na lista deixa pendente o item da regra de deliberação")]
    public void ListaDasBancasDaEtapaRecusada_ItemFicaPendente()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([]);
        EtapaProcesso[] etapas = [new ModeloDeEtapa("Redação").Adotada()];

        ItemDaDeliberacaoNoPainel(cronograma, EtapasDoProcesso.Das(etapas)).Ok.Should().BeTrue();
        ItemDaDeliberacaoNoPainel(cronograma, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].bancas"))).Ok.Should().BeFalse();
    }
}
