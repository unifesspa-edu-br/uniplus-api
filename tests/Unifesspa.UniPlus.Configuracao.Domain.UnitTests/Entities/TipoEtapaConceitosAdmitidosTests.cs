namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

/// <summary>O conjunto de tipos de conceito com que as etapas de um tipo podem ser avaliadas.</summary>
public sealed class TipoEtapaConceitosAdmitidosTests
{
    private static TipoEtapa Tipo() => TipoEtapa.Criar("PROVA", "Prova", null, true, true, false).Value!;

    private static TipoConceito Conceito(string codigo) =>
        TipoConceito.Criar(codigo, DefinicoesDeTipoConceito.Numerica()).Value!;

    [Fact(DisplayName = "O tipo nasce sem conceito admitido")]
    public void Criar_NasceSemConceitos()
    {
        Tipo().ConceitosAdmitidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Substituir define o conjunto admitido")]
    public void Substituir_DefineOConjunto()
    {
        TipoEtapa tipo = Tipo();
        TipoConceito nota = Conceito("NOTA");

        tipo.SubstituirConceitosAdmitidos([nota]);

        tipo.ConceitosAdmitidos.Should().ContainSingle().Which.Should().BeSameAs(nota);
    }

    [Fact(DisplayName = "Substituir troca o conjunto anterior pelo novo, sem acumular")]
    public void Substituir_TrocaOConjuntoAnterior()
    {
        TipoEtapa tipo = Tipo();
        TipoConceito nota = Conceito("NOTA");
        TipoConceito parecer = Conceito("PARECER");
        tipo.SubstituirConceitosAdmitidos([nota]);

        tipo.SubstituirConceitosAdmitidos([parecer]);

        tipo.ConceitosAdmitidos.Should().ContainSingle().Which.Should().BeSameAs(parecer);
    }

    [Fact(DisplayName = "Substituir por lista vazia deixa o tipo sem conceito admitido")]
    public void Substituir_ListaVazia_LimpaOConjunto()
    {
        TipoEtapa tipo = Tipo();
        tipo.SubstituirConceitosAdmitidos([Conceito("NOTA")]);

        tipo.SubstituirConceitosAdmitidos([]);

        tipo.ConceitosAdmitidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os conceitos admitidos saem ordenados pelo código")]
    public void ConceitosAdmitidos_SaemOrdenadosPeloCodigo()
    {
        TipoEtapa tipo = Tipo();

        tipo.SubstituirConceitosAdmitidos([Conceito("PARECER"), Conceito("NOTA")]);

        tipo.ConceitosAdmitidos.Select(conceito => conceito.Codigo).Should().Equal("NOTA", "PARECER");
    }
}
