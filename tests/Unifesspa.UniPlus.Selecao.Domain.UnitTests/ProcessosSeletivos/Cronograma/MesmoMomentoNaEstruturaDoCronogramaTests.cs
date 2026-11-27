namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ModeloDeFase;

/// <summary>
/// A marca "no mesmo momento da anterior" na conferência do cronograma: recusada na primeira fase e nas fases ligadas por
/// precedência, com o campo da marca na posição da fase.
/// </summary>
public sealed class MesmoMomentoNaEstruturaDoCronogramaTests
{
    private const string PrimeiraFase = "ProcessoSeletivo.PrimeiraFaseNoMesmoMomentoDaAnterior";
    private const string FasesLigadas = "ProcessoSeletivo.FasesComPrecedenciaNoMesmoMomento";

    private static (string? Campo, string Codigo)[] Estrutura(CronogramaDoProcesso cronograma) =>
        [.. cronograma.ConferirEstrutura().Select(static r => (r.Field, r.Error.Code))];

    private static (string? Campo, string Codigo)[] Precedencia(CronogramaDoProcesso cronograma, params ArestaPrecedencia[] arestas) =>
        [.. cronograma.ConferirPrecedencia(arestas).Select(static r => (r.Field, r.Error.Code))];

    private static ArestaPrecedencia Aresta(string antecessora, string sucessora) => new(antecessora, sucessora, true);

    [Fact(DisplayName = "A primeira fase marcada é recusada no campo da marca, sem identificar outra fase")]
    public void Estrutura_PrimeiraFaseMarcada_Recusa()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("A", 1).NoMesmoMomento().Declarar(), new ModeloDeFase("B", 2).Declarar());

        Estrutura(cronograma).Should().Equal(("fases[0].noMesmoMomentoDaAnterior", PrimeiraFase));
        cronograma.ConferirEstrutura().Single().Identificadores.Should().BeNull();
    }

    [Fact(DisplayName = "A primeira fase é a de menor ordem, e não a primeira da lista")]
    public void Estrutura_MenorOrdemForaDoInicioDaLista_Recusa()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("B", 2).Declarar(), new ModeloDeFase("A", 1).NoMesmoMomento().Declarar());

        Estrutura(cronograma).Should().Equal(("fases[1].noMesmoMomentoDaAnterior", PrimeiraFase));
    }

    [Fact(DisplayName = "A marca a partir da segunda fase não é recusada")]
    public void Estrutura_MarcaNaSegundaFase_NaoRecusa()
    {
        Estrutura(Cronograma(new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("B", 2).NoMesmoMomento().Declarar())).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a menor ordem repetida a recusa é só a da ordem repetida")]
    public void Estrutura_MenorOrdemRepetida_NaoEmiteARecusaDaMarca()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("A", 1).NoMesmoMomento().Declarar(), new ModeloDeFase("B", 1).Declarar());

        Estrutura(cronograma).Should().Equal(("fases[1].ordem", "ProcessoSeletivo.OrdemFaseDuplicada"));
    }

    [Fact(DisplayName = "A lista com parte recusada na leitura não afirma a primeira fase")]
    public void Estrutura_ListaComParteRecusada_NaoJulga()
    {
        CronogramaDoProcesso cronograma = new([new ModeloDeFase("A", 1).NoMesmoMomento().Declarar()]) { TemParteRecusadaNaListaDeFases = true };

        Estrutura(cronograma).Should().BeEmpty();
    }

    [Fact(DisplayName = "Fases ligadas por precedência no mesmo momento são recusadas na que vem depois, citando as duas")]
    public void Precedencia_FasesLigadasNoMesmoMomento_RecusaNaMarcada()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            (new ModeloDeFase("A", 1) with { Nome = "Primeira fase" }).Declarar(), (new ModeloDeFase("B", 2) with { Nome = "Segunda fase" }).NoMesmoMomento().Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().Equal(("fases[1].noMesmoMomentoDaAnterior", FasesLigadas));
        cronograma.ConferirPrecedencia([Aresta("A", "B")]).Single().Error.Message.Should().Contain("'Primeira fase'").And.Contain("'Segunda fase'").And.NotContain("'A'");
    }

    [Fact(DisplayName = "A primeira e a terceira de uma cadeia marcada, sem serem vizinhas, também são recusadas")]
    public void Precedencia_CadeiaMarcada_RecusaMesmoSemSeremVizinhas()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Declarar(),
            new ModeloDeFase("B", 2).NoMesmoMomento().Declarar(),
            new ModeloDeFase("C", 3).NoMesmoMomento().Declarar());

        Precedencia(cronograma, Aresta("A", "C")).Should().Equal(("fases[2].noMesmoMomentoDaAnterior", FasesLigadas));
    }

    [Fact(DisplayName = "Fases ligadas por precedência em momentos diferentes não são recusadas")]
    public void Precedencia_MomentosDiferentes_NaoRecusa()
    {
        CronogramaDoProcesso cronograma = Cronograma(
            new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("B", 2).NoMesmoMomento().Declarar(), new ModeloDeFase("C", 3).Declarar());

        Precedencia(cronograma, Aresta("A", "C"), Aresta("B", "C")).Should().BeEmpty();
    }

    [Fact(DisplayName = "A ordem invertida sem marca continua recusada como precedência violada")]
    public void Precedencia_OrdemInvertida_ContinuaPrecedenciaViolada()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("B", 1).Declarar(), new ModeloDeFase("A", 2).Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().Equal(("fases[0].ordem", "ProcessoSeletivo.PrecedenciaFaseViolada"));
    }

    [Fact(DisplayName = "A ordem invertida com a marca que junta as duas é recusada pela marca, na fase de maior ordem")]
    public void Precedencia_OrdemInvertidaEJuntaPelaMarca_RecusaNaFaseDeMaiorOrdem()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("B", 1).Declarar(), new ModeloDeFase("A", 2).NoMesmoMomento().Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().Equal(("fases[1].noMesmoMomentoDaAnterior", FasesLigadas));
    }

    [Fact(DisplayName = "Ordens iguais não emitem a recusa da marca: ficam com a precedência violada")]
    public void Precedencia_OrdensIguais_NaoEmitemARecusaDaMarca()
    {
        CronogramaDoProcesso cronograma = Cronograma(new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("B", 1).NoMesmoMomento().Declarar());

        Precedencia(cronograma, Aresta("A", "B")).Should().Equal(("fases[1].ordem", "ProcessoSeletivo.PrecedenciaFaseViolada"));
    }

    [Fact(DisplayName = "A lista com parte recusada na leitura não julga as fases ligadas no mesmo momento")]
    public void Precedencia_ListaComParteRecusada_NaoJulgaAMarca()
    {
        CronogramaDoProcesso cronograma = new([new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("B", 2).NoMesmoMomento().Declarar()])
        {
            TemParteRecusadaNaListaDeFases = true,
        };

        Precedencia(cronograma, Aresta("A", "B")).Should().BeEmpty();
    }
}
