namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

using Nivel = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas.QuemJulgaORecurso.Nivel;

/// <summary>
/// Quem julga em 1ª instância cada item recorrido, na ordem decidida: a banca com papel que julga recurso da etapa, a da fase em
/// que ela acontece e as bancas que avaliaram o item. O primeiro nível que tem banca decide.
/// </summary>
public sealed class QuemJulgaORecursoTests
{
    private const string Fase = "AVALIACAO";

    private const string NomeDaFase = "Fase de avaliação";

    private static readonly ProcedimentoDeRecursoDeclarado[] DaNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

    private static readonly TipoDeBancaCopiado Entrevista = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa);

    private static IReadOnlyList<QuemJulgaORecurso.ItemJulgado> Itens(ModeloDeFase fase, params ModeloDeEtapa[] etapas) =>
        QuemJulgaORecurso.Itens(Fases(fase), Etapas(etapas));

    private static ModeloDeFase FaseSemRecurso() => new(Fase, 1);

    [Fact(DisplayName = "O recurso da etapa é julgado pela banca de recursos da etapa, mesmo que a fase também tenha a sua")]
    public void RecursoDaEtapa_ComBancaDeRecursosNaEtapaENaFase_JulgaABancaDaEtapa()
    {
        ModeloDeFase fase = FaseSemRecurso() with { Bancas = [BancasDeTeste.DeRecursosDaFase()] };
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, BancasDeTeste.DeRecursosDaEtapa());

        Itens(fase, etapa).Single().Nivel.Should().Be(Nivel.BancaDeRecursosDaEtapa);
    }

    [Fact(DisplayName = "Sem banca de recursos própria, a banca de recursos da fase julga, e a avaliadora da etapa fica dispensada")]
    public void RecursoDaEtapa_SemBancaPropria_JulgaABancaDaFaseEADispensaAAvaliadora()
    {
        ModeloDeFase fase = FaseSemRecurso() with { Bancas = [BancasDeTeste.DeRecursosDaFase()] };
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(Entrevista));

        QuemJulgaORecurso.ItemJulgado item = Itens(fase, etapa).Single();

        item.Nivel.Should().Be(Nivel.BancaDeRecursosDaFase);
        item.Avaliadoras.Should().HaveCount(1);
        QuemJulgaORecurso.AvaliadorasQueJulgam([item]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem banca de recursos, a banca da etapa que avaliou o item julga o recurso dele")]
    public void RecursoDaEtapa_SemBancaDeRecursos_JulgaAAvaliadoraDoItem()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(Entrevista));

        QuemJulgaORecurso.ItemJulgado item = Itens(FaseSemRecurso(), etapa).Single();

        item.Nivel.Should().Be(Nivel.BancasQueAvaliaramOItem);
        QuemJulgaORecurso.AvaliadorasQueJulgam([item]).Should().BeEquivalentTo(item.Avaliadoras);
    }

    [Fact(DisplayName = "Com duas avaliadoras do item, as duas julgam: cada uma o recurso da outra")]
    public void RecursoDaEtapa_ComDuasAvaliadoras_TodasJulgam()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(Entrevista), NaEtapa(Entrevista));

        QuemJulgaORecurso.ItemJulgado item = Itens(FaseSemRecurso(), etapa).Single();

        QuemJulgaORecurso.AvaliadorasQueJulgam([item]).Should().HaveCount(2);
    }

    [Fact(DisplayName = "A banca de outro tipo de decisão não avaliou o item: o recurso fica sem banca que o julgue")]
    public void RecursoDaEtapa_AvaliadoraDeOutroTipoDeDecisao_NaoJulga()
    {
        TipoDeBancaCopiado documental = Avaliadora(TipoDeBanca.AnaliseDocumental, false, TipoMotivoDeDecisao.AnaliseDocumental);
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(documental));

        Itens(FaseSemRecurso(), etapa).Single().Nivel.Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "A cópia que lista o tipo de decisão julga, e a de outra cópia do mesmo tipo que não o lista não julga")]
    public void RecursoDaEtapa_CopiasDiferentesDoMesmoTipo_CadaBancaPelaPropriaCopia()
    {
        TipoDeBancaCopiado antiga = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa);
        TipoDeBancaCopiado nova = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa, TipoMotivoDeDecisao.AnaliseDocumental);
        ProcedimentoDeRecursoDeclarado[] daAnalise = [Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)];
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, daAnalise, false, NaEtapa(antiga), NaEtapa(nova));

        QuemJulgaORecurso.ItemJulgado item = Itens(FaseSemRecurso(), etapa).Single();

        item.Avaliadoras.Should().ContainSingle().Which.PosicaoNoTipo.Should().Be(2);
    }

    [Fact(DisplayName = "A banca com papel que avalia o mérito nunca é banca de recursos: a de importação também não julga")]
    public void RecursoDaEtapa_SoBancaDeImportacao_NinguemJulga()
    {
        ModeloDeEtapa etapa = EtapaComRecurso(
            "Redação", Fase, DaNota, false, BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.ImportacaoDeCandidatos), BancasDeTeste.Regra()));

        Itens(FaseSemRecurso(), etapa).Single().Nivel.Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "O recurso da fase sem alvo é julgado pela avaliadora do item que está só numa etapa da fase")]
    public void RecursoDaFase_SemAlvo_JulgaAAvaliadoraDeUmaEtapaDaFase()
    {
        ModeloDeFase fase = FaseComRecurso(Fase, DaNota);
        ModeloDeEtapa etapa = EtapaNaFase(Fase) with { Bancas = [NaEtapa(Entrevista)] };

        Itens(fase, etapa).Single().Nivel.Should().Be(Nivel.BancasQueAvaliaramOItem);
    }

    [Fact(DisplayName = "O recurso da fase com alvo em item de avaliação procura a avaliadora só na etapa que contém o alvo")]
    public void RecursoDaFase_ComAlvoEmItem_ProcuraSoNaEtapaDoAlvo()
    {
        Guid item = Guid.CreateVersion7();
        ProcedimentoDeRecursoDeclarado[] doItem = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(item))];
        ModeloDeEtapa doAlvo = EtapaNaFase(Fase, "Com alvo") with { Itens = [new ItemDeAvaliacaoDeclarado(item, "Item", 10m)] };
        ModeloDeEtapa outra = EtapaNaFase(Fase, "Outra") with { Bancas = [NaEtapa(Entrevista)] };

        Itens(FaseComRecurso(Fase, doItem), doAlvo, outra).Single().Nivel.Should().Be(Nivel.Ninguem);
        Itens(FaseComRecurso(Fase, doItem), doAlvo with { Bancas = [NaEtapa(Entrevista)] }, outra).Single().Nivel.Should().Be(Nivel.BancasQueAvaliaramOItem);
    }

    [Fact(DisplayName = "O recurso com alvo em área do ENEM não tem avaliadora: só a banca com papel que julga recurso o julga")]
    public void RecursoDaFase_ComAlvoEmAreaDoEnem_SoABancaDeRecursosJulga()
    {
        ProcedimentoDeRecursoDeclarado[] daArea = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica))];
        ModeloDeEtapa etapa = EtapaNaFase(Fase) with { Bancas = [NaEtapa(Entrevista)] };

        Itens(FaseComRecurso(Fase, daArea), etapa).Single().Nivel.Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "O recurso sem procedimento só tem a banca com papel que julga recurso: a avaliadora não basta")]
    public void RecursoSemProcedimento_SoABancaDeRecursosJulga()
    {
        ModeloDeEtapa semProcedimento = EtapaComRecurso("Redação", Fase, [], false, NaEtapa(Entrevista));
        ModeloDeEtapa comDedicada = EtapaComRecurso("Prova", Fase, [], false, BancasDeTeste.DeRecursosDaEtapa());

        Itens(FaseSemRecurso(), semProcedimento).Single().Nivel.Should().Be(Nivel.Ninguem);
        Itens(FaseSemRecurso(), comDedicada).Single().Nivel.Should().Be(Nivel.BancaDeRecursosDaEtapa);
    }

    [Fact(DisplayName = "Cada procedimento é um item, na ordem canônica, e os da fase vêm antes dos das etapas")]
    public void Itens_UmPorProcedimento_FasesAntesDasEtapas()
    {
        ProcedimentoDeRecursoDeclarado[] doisTipos = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa), Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)];
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota);

        Itens(FaseComRecurso(Fase, doisTipos), etapa)
            .Select(static item => (item.Recurso.Onde, item.Procedimento?.TipoDeDecisao))
            .Should().Equal(
                ($"fase '{NomeDaFase}'", TipoMotivoDeDecisao.AnaliseDocumental),
                ($"fase '{NomeDaFase}'", TipoMotivoDeDecisao.NotaDaEtapa),
                ("etapa na posição 1", TipoMotivoDeDecisao.NotaDaEtapa));
    }

    [Fact(DisplayName = "As etapas julgadas pela banca da fase são as de recurso julgado nesse nível, na fase dela, sem repetir")]
    public void EtapasJulgadasPelaFase_SoAsDoNivelDaFase()
    {
        ModeloDeFase fase = FaseSemRecurso() with { Bancas = [BancasDeTeste.DeRecursosDaFase()] };
        ModeloDeEtapa daFase = EtapaComRecurso("Redação", Fase, [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa), Procedimento(TipoMotivoDeDecisao.Classificacao)]);
        ModeloDeEtapa propria = EtapaComRecurso("Prova", Fase, DaNota, false, BancasDeTeste.DeRecursosDaEtapa());
        CronogramaDoProcesso cronograma = Fases(fase);

        IReadOnlyList<QuemJulgaORecurso.ItemJulgado> itens = QuemJulgaORecurso.Itens(cronograma, Etapas(daFase, propria));

        QuemJulgaORecurso.EtapasJulgadasPelaFase(itens, cronograma.Fases[0]).Select(static etapa => etapa.Posicao).Should().Equal(0);
    }
}
