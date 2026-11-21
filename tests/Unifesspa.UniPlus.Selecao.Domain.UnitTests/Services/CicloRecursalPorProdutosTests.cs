namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

using Xunit;

/// <summary>As consultas do ciclo recursal sobre os pares de ato e papel que um lugar publica, sem a entidade.</summary>
public sealed class CicloRecursalPorProdutosTests
{
    private static readonly (string AtoCodigo, PapelProdutoFase? Papel)[] Produtos =
    [
        ("GABARITO", PapelProdutoFase.Preliminar),
        ("GABARITO", PapelProdutoFase.Definitivo),
        ("RESULTADO", PapelProdutoFase.Preliminar),
        ("COMUNICADO", null),
    ];

    [Fact(DisplayName = "Publica com papel quando algum produto tem o papel pedido")]
    public void PublicaComPapel_AlgumProdutoTemOPapel()
    {
        CicloRecursal.PublicaComPapel(Produtos, PapelProdutoFase.Definitivo).Should().BeTrue();
        CicloRecursal.PublicaComPapel([("COMUNICADO", null)], PapelProdutoFase.Preliminar).Should().BeFalse();
    }

    [Fact(DisplayName = "Produz resultado quando algum produto tem papel, e o ato sem papel não produz")]
    public void ProduzResultado_ExigePapel()
    {
        CicloRecursal.ProduzResultado(Produtos).Should().BeTrue();
        CicloRecursal.ProduzResultado([("COMUNICADO", null)]).Should().BeFalse();
        CicloRecursal.ProduzResultado([]).Should().BeFalse();
    }

    [Fact(DisplayName = "Os tipos com ciclo aberto são os preliminares sem definitivo do mesmo tipo")]
    public void TiposComCicloAberto_PreliminarSemDefinitivo()
    {
        CicloRecursal.TiposComCicloAberto(Produtos).Should().Equal("RESULTADO");
    }
}
