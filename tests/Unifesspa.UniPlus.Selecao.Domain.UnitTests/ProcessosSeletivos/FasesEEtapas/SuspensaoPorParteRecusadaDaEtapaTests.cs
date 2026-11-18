namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A regra que quantifica sobre uma lista da etapa em que a leitura do documento recusou alguma parte não julga a etapa: o que
/// falta na lista pode ser o que a regra exige. As demais etapas seguem julgadas, e o item do painel fica pendente em vez de
/// aprovado por omissão.
/// </summary>
public sealed class SuspensaoPorParteRecusadaDaEtapaTests
{
    private static EtapaProcesso EtapaComPreliminarSemDefinitivo(string nome, int ordem) =>
        new ModeloDeEtapa(nome) { Ordem = ordem, Produtos = [ModeloDeEtapa.Preliminar("RESULTADO_PROVA")] }.Adotada();

    [Fact(DisplayName = "O preliminar sem definitivo não é julgado na etapa cuja lista dos produtos tem parte recusada, e a outra etapa segue julgada")]
    public void PreliminarSemDefinitivo_ComParteRecusadaNosProdutos_SuspendeSoNaEtapaDaLista()
    {
        EtapaProcesso[] etapas = [EtapaComPreliminarSemDefinitivo("Prova", 1), EtapaComPreliminarSemDefinitivo("Redação", 2)];

        EtapasDoProcesso lidas = EtapasDoProcesso.Das(etapas);
        EtapasDoProcesso comParteRecusada = EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].produtos"));

        lidas.ConferirFechamentoDoCiclo().Select(static recusa => recusa.Field).Should().Equal("etapas[0].produtos", "etapas[1].produtos");
        comParteRecusada.ConferirFechamentoDoCiclo().Select(static recusa => recusa.Field).Should().Equal("etapas[1].produtos");
    }

    [Fact(DisplayName = "O item do ciclo recursal fica pendente, e não aprovado, quando a lista dos produtos de uma etapa tem parte recusada")]
    public void ItemDoCicloRecursal_ComParteRecusadaNosProdutosDaEtapa_FicaPendente()
    {
        EtapaProcesso[] etapas = [new ModeloDeEtapa("Prova") { Ordem = 1 }.Adotada()];
        CronogramaDoProcesso semFases = CronogramaDoProcesso.Do([new ModeloDeFase("INSCRICAO", 1).Janela(1, 5).Adotada()]);

        CicloRecursalDoProcesso.Item(semFases, EtapasDoProcesso.Das(etapas)).Ok.Should().BeTrue();
        CicloRecursalDoProcesso.Item(semFases, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].produtos"))).Ok.Should().BeFalse();
    }

    [Theory(DisplayName = "A âncora da janela de recurso que não é produto preliminar não é julgada quando a lista dos produtos ou a dos recursos tem parte recusada")]
    [InlineData(null)]
    [InlineData("etapas[0].produtos")]
    [InlineData("etapas[0].recursos")]
    public void AncoraDaJanelaDeRecurso_ComParteRecusada_NaoEJulgada(string? colecaoRecusada)
    {
        EtapaProcesso etapa = new ModeloDeEtapa("Prova") { Recursos = [RecursoModelo.AoAto(Guid.CreateVersion7())] }.Adotada();
        OrigemNoDocumento origem = colecaoRecusada is null ? OrigemNoDocumento.Identidade : ComPartesRecusadas(colecaoRecusada);

        int recusas = EtapasDoProcesso.Das([etapa], origem).RecusasDasDeclaracoes().Count;

        recusas.Should().Be(colecaoRecusada is null ? 1 : 0);
    }

    [Fact(DisplayName = "O item da etapa humana sem banca fica pendente, e não aprovado, quando a lista das bancas da etapa tem parte recusada")]
    public void ItemDaEtapaHumanaSemBanca_ComParteRecusadaNasBancas_FicaPendente()
    {
        EtapaProcesso[] etapas = [new ModeloDeEtapa("Redação") { Ordem = 1 }.Humana().Adotada()];

        ItemDaBanca(EtapasDoProcesso.Das(etapas)).Ok.Should().BeTrue();
        ItemDaBanca(EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].bancas"))).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa humana sem banca e sem parte recusada continua reprovada")]
    public void ItemDaEtapaHumanaSemBanca_SemBancas_ContinuaReprovado()
    {
        EtapaProcesso etapa = (new ModeloDeEtapa("Redação") { Ordem = 1 }.Humana() with { Bancas = [] }).Adotada();

        ItemDaBanca(EtapasDoProcesso.Das([etapa])).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O recurso sem banca de análise não é julgado na etapa cuja lista das bancas ou dos recursos tem parte recusada, nem na que a fase com lista recusada julgaria")]
    public void RecursoSemBanca_ComParteRecusadaNaEtapaOuNaFase_NaoEJulgado()
    {
        EtapaProcesso[] etapas = [new ModeloDeEtapa("Prova") { Ordem = 1, FaseCodigo = "RESULTADO", Recursos = [RecursoModelo.NaCiencia()], EmiteParecerIndividual = true }.Adotada()];
        CronogramaDoProcesso fase = CronogramaDoProcesso.Do([new ModeloDeFase("RESULTADO", 1).Janela(1, 5).Adotada()]);
        CronogramaDoProcesso faseComListaRecusada = CronogramaDoProcesso.Do([new ModeloDeFase("RESULTADO", 1).Janela(1, 5).Adotada()], ComPartesRecusadas("fases[0].bancasRequeridas"));

        ConferenciaDasBancas.ConferirRecursosSemBanca(fase, EtapasDoProcesso.Das(etapas)).Should().ContainSingle();
        ConferenciaDasBancas.ConferirRecursosSemBanca(fase, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].bancas"))).Should().BeEmpty();
        ConferenciaDasBancas.ConferirRecursosSemBanca(fase, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].recursos"))).Should().BeEmpty();
        ConferenciaDasBancas.ConferirRecursosSemBanca(faseComListaRecusada, EtapasDoProcesso.Das(etapas)).Should().BeEmpty();
    }

    private static ItemConformidade ItemDaBanca(EtapasDoProcesso etapas) =>
        etapas.ItensDaAvaliacao(CitacoesDeNota.Nenhuma).Single(static item => item.Codigo == "etapa_humana_sem_banca");
}
