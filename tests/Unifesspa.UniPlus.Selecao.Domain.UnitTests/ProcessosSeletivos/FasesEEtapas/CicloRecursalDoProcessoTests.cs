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

/// <summary>O ciclo recursal do processo junta o das fases e o das etapas: as fases primeiro, e cada etapa fecha o ciclo dentro dela.</summary>
public sealed class CicloRecursalDoProcessoTests
{
    [Fact(DisplayName = "As causas das fases saem antes das das etapas, cada uma na posição de quem a causa")]
    public void Conferir_FasesEDepoisEtapas()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("RESULTADO", 1).Publicando(ModeloDeFase.Preliminar("RESULTADO")));
        EtapasDoProcesso etapas = Etapas(new ModeloDeEtapa("Prova") { Produtos = [ModeloDeEtapa.Preliminar("GABARITO")] });

        Causas(CicloRecursalDoProcesso.Conferir(cronograma, etapas)).Should().Equal(
            ("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoNaoDeclarada"),
            ("etapas[0].produtos", "ProcessoSeletivo.EtapaComPreliminarSemDefinitivo"));
    }

    [Fact(DisplayName = "O ciclo fechado na própria fase e na própria etapa não tem causa, e o item fica verde")]
    public void Conferir_CicloFechado_SemCausa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("RESULTADO", 1).Publicando(ModeloDeFase.Preliminar("RESULTADO"), ModeloDeFase.Definitivo("RESULTADO")));
        EtapasDoProcesso etapas = Etapas(new ModeloDeEtapa("Prova") { Produtos = [ModeloDeEtapa.Preliminar("GABARITO"), ModeloDeEtapa.Definitivo("GABARITO")] });

        CicloRecursalDoProcesso.Conferir(cronograma, etapas).Should().BeEmpty();
        CicloRecursalDoProcesso.Item(cronograma, etapas).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "O item do ciclo fica vermelho com causa na fase ou na etapa, e tem o código e o texto do checklist")]
    public void Item_CausaNaFaseOuNaEtapa_Vermelho()
    {
        CronogramaDoProcesso conforme = Fases(new ModeloDeFase("INSCRICAO", 1));
        CronogramaDoProcesso comCausa = Fases(new ModeloDeFase("RESULTADO", 1).Publicando(ModeloDeFase.Preliminar("RESULTADO")));
        EtapasDoProcesso etapaComCausa = Etapas(new ModeloDeEtapa("Prova") { Produtos = [ModeloDeEtapa.Preliminar("GABARITO")] });

        CicloRecursalDoProcesso.Item(comCausa, Etapas()).Ok.Should().BeFalse();
        CicloRecursalDoProcesso.Item(conforme, etapaComCausa).Ok.Should().BeFalse();
        CicloRecursalDoProcesso.Item(conforme, Etapas()).Codigo.Should().Be("cronograma_conclusao_do_ciclo_recursal");
        CicloRecursalDoProcesso.Item(conforme, Etapas()).Mensagem.Should().StartWith("Cronograma: a fase ou a etapa que publica resultado preliminar");
    }

    [Fact(DisplayName = "A concluinte de código compartilhado por duas fases deixa o item vermelho e ecoa a fase que a declara")]
    public void Item_ConcluinteDeCodigoAmbiguo_Vermelho()
    {
        CronogramaDoProcesso cronograma = Fases(
            new ModeloDeFase("ANALISE", 1) { FaseConcluinteCodigo = "RESULTADO" }.Publicando(ModeloDeFase.Preliminar("RESULTADO")),
            new ModeloDeFase("RESULTADO", 2).Publicando(ModeloDeFase.Definitivo("RESULTADO")),
            new ModeloDeFase("RESULTADO", 3).Publicando(ModeloDeFase.Definitivo("RESULTADO")));

        CicloRecursalDoProcesso.Conferir(cronograma, Etapas()).Should().BeEmpty();

        ItemConformidade item = CicloRecursalDoProcesso.Item(cronograma, Etapas());

        item.Ok.Should().BeFalse();
        item.Mensagem.Should().EndWith(" (fases com concluinte indeterminada: ANALISE)");
    }

    [Fact(DisplayName = "A janela invertida de uma das duas fases, que impede a comparação de início, deixa o item vermelho")]
    public void Item_JanelaInvertidaImpedeAComparacao_Vermelho()
    {
        ModeloDeFase abre = new ModeloDeFase("ANALISE", 1) { FaseConcluinteCodigo = "RESULTADO" }.Publicando(ModeloDeFase.Preliminar("RESULTADO"));
        ModeloDeFase conclui = new ModeloDeFase("RESULTADO", 2).Publicando(ModeloDeFase.Definitivo("RESULTADO"));

        CronogramaDoProcesso invertidaNaConcluinte = Fases(abre.Janela(5, 6), conclui.Janela(9, 4));
        CronogramaDoProcesso invertidaNaQueAbre = Fases(abre.Janela(8, 2), conclui.Janela(3, 9));

        CicloRecursalDoProcesso.Conferir(invertidaNaConcluinte, Etapas()).Should().BeEmpty();
        CicloRecursalDoProcesso.Item(invertidaNaConcluinte, Etapas()).Ok.Should().BeFalse();
        CicloRecursalDoProcesso.Item(invertidaNaQueAbre, Etapas()).Mensagem.Should().EndWith(" (fases com concluinte indeterminada: ANALISE)");
    }

    [Fact(DisplayName = "A concluinte localizada com as janelas comparáveis, ou sem janela declarada, mantém o item verde sem eco")]
    public void Item_ConcluinteDeterminada_Verde()
    {
        ModeloDeFase abre = new ModeloDeFase("ANALISE", 1) { FaseConcluinteCodigo = "RESULTADO" }.Publicando(ModeloDeFase.Preliminar("RESULTADO"));
        ModeloDeFase conclui = new ModeloDeFase("RESULTADO", 2).Publicando(ModeloDeFase.Definitivo("RESULTADO"));

        CicloRecursalDoProcesso.Item(Fases(abre.Janela(5, 6), conclui.Janela(5, 9)), Etapas()).Ok.Should().BeTrue();
        CicloRecursalDoProcesso.Item(Fases(abre, conclui), Etapas()).Ok.Should().BeTrue();
        CicloRecursalDoProcesso.Item(Fases(abre, conclui), Etapas()).Mensagem.Should().NotContain("indeterminada");
    }
}
