namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

using static ModeloDeFase;

public sealed class ConclusaoDoCicloRecursalTests
{
    private const string Resultado = "RESULTADO";

    private static (string? Campo, string Codigo)[] Conclusao(params FaseDeclarada[] fases) =>
        [.. Cronograma(fases).ConferirConclusaoDoCiclo().Select(static r => (r.Field, r.Error.Code))];

    private static ModeloDeFase AbreOCiclo(string codigo, int ordem, string? concluinte = null) =>
        new ModeloDeFase(codigo, ordem) { FaseConcluinteCodigo = concluinte }.Publicando(Preliminar(Resultado));

    private static ModeloDeFase Conclui(string codigo, int ordem, string tipo = Resultado) =>
        new ModeloDeFase(codigo, ordem).Publicando(Definitivo(tipo));

    [Fact(DisplayName = "Fase que fecha o ciclo na própria fase não declara concluinte e não é recusada")]
    public void Conferir_FaseQueFechaASiMesma_NaoRecusa()
    {
        Conclusao(new ModeloDeFase("A", 1).Publicando(Preliminar(Resultado), Definitivo(Resultado)).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Concluinte declarada por fase que não publica preliminar é recusada")]
    public void Conferir_ConclusaoSemPreliminar_Recusa()
    {
        Conclusao(new ModeloDeFase("A", 1) { FaseConcluinteCodigo = "B" }.Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar"));
    }

    [Fact(DisplayName = "Preliminar sem definitivo do mesmo tipo e sem concluinte declarada é recusado")]
    public void Conferir_ConclusaoNaoDeclarada_Recusa()
    {
        Conclusao(AbreOCiclo("A", 1).Declarar()).Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoNaoDeclarada"));
    }

    [Fact(DisplayName = "Fase que declara a si mesma como concluinte é recusada")]
    public void Conferir_ConcluinteIgualAFase_Recusa()
    {
        Conclusao(AbreOCiclo("A", 1, concluinte: "A").Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui"));
    }

    [Fact(DisplayName = "Concluinte que não está no cronograma é recusada")]
    public void Conferir_ConcluinteForaDoCronograma_Recusa()
    {
        Conclusao(AbreOCiclo("A", 1, concluinte: "B").Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteForaDoCronograma"));
    }

    [Fact(DisplayName = "Concluinte sem nenhum definitivo e concluinte sem o definitivo do tipo são recusadas")]
    public void Conferir_ConcluinteSemDefinitivo_Recusa()
    {
        Conclusao(AbreOCiclo("A", 1, "B").Declarar(), new ModeloDeFase("B", 2).Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo"));
        Conclusao(AbreOCiclo("A", 1, "B").Declarar(), Conclui("B", 2, tipo: "GABARITO").Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo"));
    }

    [Fact(DisplayName = "Dois definitivos para o mesmo ciclo são recusados")]
    public void Conferir_DoisDefinitivos_Recusa()
    {
        FaseDeclarada fechaSozinha = new ModeloDeFase("A", 1) { FaseConcluinteCodigo = "B" }
            .Publicando(Preliminar(Resultado), Definitivo(Resultado), Preliminar("GABARITO")).Declarar();

        Conclusao(fechaSozinha, new ModeloDeFase("B", 2).Publicando(Definitivo(Resultado), Definitivo("GABARITO")).Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoDuplicada"));
    }

    [Fact(DisplayName = "Concluinte com ordem igual ou menor que a da fase concluída é recusada")]
    public void Conferir_ConcluinteQueAntecede_Recusa()
    {
        Conclusao(AbreOCiclo("A", 2, "B").Declarar(), Conclui("B", 2).Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteAntecedeAConcluida"));
    }

    [Fact(DisplayName = "Concluinte que começa antes da fase concluída é recusada, e começar no mesmo instante não")]
    public void Conferir_ConcluinteQueComecaAntes_RecusaSoNaFronteira()
    {
        Conclusao(AbreOCiclo("A", 1, "B").Janela(5, 6).Declarar(), Conclui("B", 2).Janela(4, 9).Declarar())
            .Should().Equal(("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida"));
        Conclusao(AbreOCiclo("A", 1, "B").Janela(5, 6).Declarar(), Conclui("B", 2).Janela(5, 9).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Janela invertida em uma das duas fases corta a comparação de início")]
    public void Conferir_JanelaInvertida_CortaAComparacaoDeInicio()
    {
        Conclusao(AbreOCiclo("A", 1, "B").Janela(5, 6).Declarar(), Conclui("B", 2).Janela(9, 4).Declarar()).Should().BeEmpty();
        Conclusao(AbreOCiclo("A", 1, "B").Janela(8, 2).Declarar(), Conclui("B", 2).Janela(3, 9).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Concluinte com código ambíguo suspende só a fase concluída")]
    public void Conferir_ConcluinteAmbigua_SuspendeSoAFaseConcluida()
    {
        Conclusao(
            AbreOCiclo("A", 1, "B").Declarar(),
            Conclui("B", 2).Declarar(),
            Conclui("B", 3, tipo: "GABARITO").Declarar(),
            AbreOCiclo("C", 4).Declarar())
            .Should().Equal(("fases[3].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoNaoDeclarada"));
    }

    [Fact(DisplayName = "Entre fases não há encadeamento: toda fase mal declarada é recusada na sua posição")]
    public void Conferir_VariasFasesMalDeclaradas_RecusaCadaUma()
    {
        Conclusao(AbreOCiclo("A", 1).Declarar(), AbreOCiclo("B", 2, "ZZ").Declarar()).Should().Equal(
            ("fases[0].faseConcluinteCodigo", "ProcessoSeletivo.ConclusaoNaoDeclarada"),
            ("fases[1].faseConcluinteCodigo", "ProcessoSeletivo.FaseConcluinteForaDoCronograma"));
    }
}
