namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static CenarioDeProcesso;

/// <summary>A etapa acontece numa fase do cronograma: a fase existe, a janela da etapa cabe na dela e a fase que agrupa etapas tem alguma.</summary>
public sealed class EtapasNoCronogramaTests
{
    private static readonly DateTimeOffset Abertura = ModeloDeFase.Abertura;

    private static ModeloDeFase Avaliacao() => new ModeloDeFase("AVALIACAO", 2).Janela(0, 10);

    private static (string? Campo, string Codigo)[] Janela(ModeloDeEtapa etapa, ModeloDeFase? fase = null) =>
        Causas(EtapasNoCronograma.ConferirJanelaDasEtapas(Fases(fase ?? Avaliacao()), Etapas(etapa)));

    private static ModeloDeEtapa Datada(DateTimeOffset? inicio, DateTimeOffset? fim) =>
        EtapaNaFase("AVALIACAO") with { Inicio = inicio, Fim = fim };

    [Fact(DisplayName = "A etapa dentro da janela da fase, com as bordas coincidentes, não tem causa")]
    public void Janela_DentroDaFaseComBordasCoincidentes_SemCausa()
    {
        Janela(Datada(Abertura, Abertura.AddDays(10))).Should().BeEmpty();
        Janela(Datada(Abertura.AddDays(2), Abertura.AddDays(3))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa que começa antes da fase é recusada no início")]
    public void Janela_ComecaAntesDaFase_RecusaNoInicio()
    {
        Janela(Datada(Abertura.AddDays(-1), null)).Should().Equal(("etapas[0].inicio", "ProcessoSeletivo.EtapaComecaAntesDaFase"));
    }

    [Fact(DisplayName = "A etapa que começa depois do fim da fase é recusada no início como fora da janela")]
    public void Janela_ComecaDepoisDoFimDaFase_RecusaNoInicio()
    {
        Janela(Datada(Abertura.AddDays(11), null)).Should().Equal(("etapas[0].inicio", "ProcessoSeletivo.EtapaForaDaJanelaDaFase"));
    }

    [Fact(DisplayName = "A etapa que termina depois da fase é recusada no fim")]
    public void Janela_TerminaDepoisDaFase_RecusaNoFim()
    {
        Janela(Datada(null, Abertura.AddDays(11))).Should().Equal(("etapas[0].fim", "ProcessoSeletivo.EtapaTerminaDepoisDaFase"));
    }

    [Fact(DisplayName = "A etapa que termina antes de a fase começar é recusada no fim como fora da janela")]
    public void Janela_TerminaAntesDaFase_RecusaNoFim()
    {
        Janela(Datada(null, Abertura.AddDays(-1))).Should().Equal(("etapas[0].fim", "ProcessoSeletivo.EtapaForaDaJanelaDaFase"));
    }

    [Fact(DisplayName = "A etapa que sai da fase pelos dois extremos tem as duas causas, o início primeiro")]
    public void Janela_ForaPelosDoisExtremos_InicioEDepoisFim()
    {
        Janela(Datada(Abertura.AddDays(-1), Abertura.AddDays(11))).Should().Equal(
            ("etapas[0].inicio", "ProcessoSeletivo.EtapaComecaAntesDaFase"),
            ("etapas[0].fim", "ProcessoSeletivo.EtapaTerminaDepoisDaFase"));
    }

    [Fact(DisplayName = "A mensagem da janela cita a etapa, a fase e os instantes em UTC")]
    public void Janela_Mensagem_CitaEtapaFaseEInstantes()
    {
        string mensagem = EtapasNoCronograma.ConferirJanelaDasEtapas(Fases(Avaliacao()), Etapas(Datada(Abertura.AddDays(-1), null))).Single().Error.Message;

        mensagem.Should().Contain("\"Prova\"").And.Contain("2026-03-01T12:00:00.0000000+00:00").And.Contain("AVALIACAO");
    }

    [Fact(DisplayName = "A fase sem extremo não limita a etapa, e a etapa sem janela acontece na da fase")]
    public void Janela_ExtremoAusenteNaoLimita()
    {
        Janela(Datada(Abertura.AddDays(-30), Abertura.AddDays(30)), new ModeloDeFase("AVALIACAO", 2)).Should().BeEmpty();
        Janela(Datada(null, Abertura.AddDays(30)), new ModeloDeFase("AVALIACAO", 2) with { Inicio = Abertura }).Should().BeEmpty();
        Janela(Datada(null, null)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As causas saem na ordem das etapas")]
    public void Janela_VariasEtapas_NaOrdemDasPosicoes()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao());
        EtapasDoProcesso etapas = Etapas(Datada(Abertura.AddDays(11), null), EtapaNaFase("AVALIACAO", "Dentro"), Datada(null, Abertura.AddDays(12)));

        Causas(EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas)).Select(static causa => causa.Campo)
            .Should().Equal("etapas[0].inicio", "etapas[2].fim");
    }

    [Fact(DisplayName = "A fase com janela invertida corta a conferência da etapa, e o item fica vermelho")]
    public void Janela_FaseInvertida_CortaEDeixaOItemVermelho()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 2).Janela(10, 0));
        EtapasDoProcesso etapas = Etapas(Datada(Abertura.AddDays(-5), null));

        EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas).Should().BeEmpty();
        EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O código repetido no cronograma corta a conferência da etapa, e o item fica vermelho")]
    public void Janela_CodigoAmbiguo_CortaEDeixaOItemVermelho()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao(), new ModeloDeFase("AVALIACAO", 3).Janela(0, 10));
        EtapasDoProcesso etapas = Etapas(Datada(Abertura.AddDays(-5), null));

        EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas).Should().BeEmpty();
        EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O item da janela da etapa é verde quando toda etapa cabe na fase, e a etapa sem janela não o deixa vermelho")]
    public void ItemDaJanela_EtapasConformes_Verde()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 2).Janela(10, 0));
        ItemConformidade item = EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, Etapas(EtapaNaFase("AVALIACAO")));

        item.Codigo.Should().Be("cronograma_etapa_fora_da_janela_da_fase");
        item.Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Fase presente: verde, e a etapa que começa antes da fase tem só a causa da janela")]
    public void Estado1_FasePresente_VerdeESoAJanela()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao());
        EtapasDoProcesso conforme = Etapas(EtapaNaFase("AVALIACAO"));
        EtapasDoProcesso antes = Etapas(Datada(Abertura.AddDays(-1), null));

        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, conforme).Ok.Should().BeTrue();
        EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, antes).Should().BeEmpty();
        antes.ConferirCompletude().Should().BeEmpty();
        Causas(EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, antes)).Select(static causa => causa.Codigo)
            .Should().Equal("ProcessoSeletivo.EtapaComecaAntesDaFase");
    }

    [Fact(DisplayName = "Fase nula: só a causa da etapa sem fase declarada, e o item fica vermelho")]
    public void Estado2_FaseNula_SoEtapaSemFaseDeclarada()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao());
        EtapasDoProcesso etapas = Etapas(new ModeloDeEtapa("Prova"));

        EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas).Should().BeEmpty();
        EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas).Should().BeEmpty();
        Causas(etapas.ConferirCompletude()).Should().Equal(("etapas[0].faseCodigo", "ProcessoSeletivo.EtapaSemFaseDeclarada"));
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Fase ausente do cronograma: só a causa do furo, sem causa de janela, e o item fica vermelho")]
    public void Estado3_FaseAusenteDoCronograma_SoOFuro()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao());
        EtapasDoProcesso etapas = Etapas(EtapaNaFase("ENTREVISTA") with { Inicio = Abertura.AddDays(-30) });

        Causas(EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas)).Should().Equal(("etapas[0].faseCodigo", "ProcessoSeletivo.EtapaSemFaseNoCronograma"));
        EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas).Single().Error.Message.Should().Be("A etapa \"Prova\" declara a fase ENTREVISTA, que não está no cronograma deste processo.");
        EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas).Should().BeEmpty();
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A fase com código repetido existe: a etapa que a declara não tem o furo")]
    public void Furo_CodigoRepetido_NaoEFuro()
    {
        CronogramaDoProcesso cronograma = Fases(Avaliacao(), new ModeloDeFase("AVALIACAO", 3));

        EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, Etapas(EtapaNaFase("AVALIACAO"))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que agrupa etapas sem nenhuma etapa que declare acontecer nela é recusada, uma causa por fase")]
    public void Agrupadora_SemEtapaNela_RecusaPorFase()
    {
        CronogramaDoProcesso cronograma = Fases(
            new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true },
            new ModeloDeFase("ENTREVISTAS", 2) with { AgrupaEtapas = true },
            new ModeloDeFase("RESULTADO", 3));
        EtapasDoProcesso etapas = Etapas(EtapaNaFase("PROVAS"));

        Causas(EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas))
            .Should().Equal(("fases[1].faseCanonicaId", "ProcessoSeletivo.AvaliacaoSemEtapa"));
        EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas).Single().Error.Message
            .Should().Be("A fase 'ENTREVISTAS' agrupa etapas no cronograma, e nenhuma etapa declara acontecer nela.");
    }

    [Fact(DisplayName = "Com etapa sem fase declarada a regra da fase que agrupa etapas é cortada: nenhuma recusa, o item da agrupadora e o da fase declarada ficam vermelhos")]
    public void Agrupadora_ComEtapaSemFase_CortaARegraEDeixaOsItensVermelhos()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true });
        EtapasDoProcesso etapas = Etapas(new ModeloDeEtapa("Prova"));

        EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas).Should().BeEmpty();
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(cronograma, etapas).Ok.Should().BeFalse();
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Sem nenhuma etapa a fase que agrupa etapas é recusada")]
    public void Agrupadora_SemNenhumaEtapa_Recusa() =>
        EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(Fases(new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true }), Etapas()).Should().HaveCount(1);

    [Fact(DisplayName = "A fase que agrupa etapas com código repetido é cortada, e o item fica vermelho")]
    public void Agrupadora_CodigoAmbiguo_CortaEDeixaOItemVermelho()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true }, new ModeloDeFase("PROVAS", 2));
        EtapasDoProcesso semEtapa = Etapas();

        EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, semEtapa).Should().BeEmpty();
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(cronograma, semEtapa).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O item da fase que agrupa etapas é verde com etapa nela, e a fase que não agrupa não exige etapa")]
    public void ItemDaAgrupadora_VerdeComEtapaOuSemAgrupar()
    {
        ItemConformidade verde = EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(
            Fases(new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true }), Etapas(EtapaNaFase("PROVAS")));
        ItemConformidade vermelho = EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(
            Fases(new ModeloDeFase("PROVAS", 1) with { AgrupaEtapas = true }), Etapas());

        verde.Codigo.Should().Be("cronograma_fase_agrupadora_sem_etapa_pontuada");
        verde.Mensagem.Should().Be("Cronograma: toda fase que agrupa etapas tem ao menos uma etapa que acontece nela");
        verde.Ok.Should().BeTrue();
        vermelho.Ok.Should().BeFalse();
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(Fases(new ModeloDeFase("PROVAS", 1)), Etapas()).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "O item da fase declarada pela etapa tem o código e o texto do checklist")]
    public void ItemDaFaseDeclarada_CodigoETexto()
    {
        ItemConformidade item = EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(Fases(Avaliacao()), Etapas());

        item.Codigo.Should().Be("cronograma_etapa_declara_a_fase");
        item.Mensagem.Should().Be("Cronograma: toda etapa declara uma fase que está no cronograma");
        item.Ok.Should().BeTrue();
    }
}
