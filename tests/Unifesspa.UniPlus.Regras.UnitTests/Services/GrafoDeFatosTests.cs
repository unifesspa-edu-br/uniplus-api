namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;

public sealed class GrafoDeFatosTests
{
    [Fact]
    public void Ordenar_MantemAOrdemDeEntrada_EAntecipaQuemTemMenorPrioridadeAssimQueFicaPronto()
    {
        OrdemTopologica ordem = GrafoDeFatos.Ordenar(
        [
            new NoDoGrafo("PCD", [], 0),
            new NoDoGrafo("NOME", [], 1),
            new NoDoGrafo("LAUDO", ["MODALIDADE"], 2),
            new NoDoGrafo("MODALIDADE", ["PCD"], -1),
        ]);

        ordem.Ordem.Should().Equal("PCD", "MODALIDADE", "NOME", "LAUDO");
        ordem.ForaDeOrdem.Should().BeEmpty();
    }

    [Fact]
    public void Ordenar_DeixaDeFora_OsNosEmCicloEQuemDependeDeles()
    {
        OrdemTopologica ordem = GrafoDeFatos.Ordenar(
        [
            new NoDoGrafo("A", ["B"], 0),
            new NoDoGrafo("B", ["A"], 1),
            new NoDoGrafo("C", ["A"], 2),
            new NoDoGrafo("D", ["EXTERNO"], 3),
        ]);

        ordem.Ordem.Should().Equal("D");
        ordem.ForaDeOrdem.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void DetectarCiclo_DevolveOCaminhoFechado()
    {
        Dictionary<string, IReadOnlyCollection<string>> citacoes = new(StringComparer.Ordinal)
        {
            ["A"] = ["B"],
            ["B"] = ["C"],
            ["C"] = ["A"],
        };

        GrafoDeFatos.DetectarCiclo(citacoes).Should().Equal("A", "B", "C", "A");
    }
}
