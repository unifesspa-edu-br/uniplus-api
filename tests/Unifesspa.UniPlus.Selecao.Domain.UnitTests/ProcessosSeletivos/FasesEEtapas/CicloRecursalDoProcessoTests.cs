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

/// <summary>O fechamento do ciclo de recurso do processo junta o das fases e o das etapas: as fases primeiro, e cada fase e cada etapa fecham o ciclo dentro de si.</summary>
public sealed class CicloRecursalDoProcessoTests
{
    [Fact(DisplayName = "As causas das fases saem antes das das etapas, cada uma na posição de quem a causa")]
    public void Conferir_FasesEDepoisEtapas()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("RESULTADO", 1).Publicando(ModeloDeFase.Preliminar("RESULTADO")));
        EtapasDoProcesso etapas = Etapas(new ModeloDeEtapa("Prova") { Produtos = [ModeloDeEtapa.Preliminar("GABARITO")] });

        Causas(CicloRecursalDoProcesso.Conferir(cronograma, etapas)).Should().Equal(
            ("fases[0].produtos", "ProcessoSeletivo.FaseComPreliminarSemDefinitivo"),
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
}
