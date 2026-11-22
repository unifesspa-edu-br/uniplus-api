namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class FormaDeComprovacaoTests
{
    [Theory]
    [InlineData(FormaDeComprovacao.Documento, "DOCUMENTO")]
    [InlineData(FormaDeComprovacao.TodosOsItens, "TODOS_OS_ITENS")]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens, "QUANTIDADE_MINIMA_DE_ITENS")]
    public void Codigo_ParaCadaForma_UsaOCodigoDeclarado(FormaDeComprovacao forma, string codigo)
    {
        forma.Codigo().Should().Be(codigo);
        CatalogoDe<FormaDeComprovacao>.Obter(codigo).Should().Be(forma);
    }

    [Theory]
    [InlineData("FOLHA")]
    [InlineData("E")]
    [InlineData("OU")]
    [InlineData("INVENTADA")]
    public void TentarObter_CodigoAntigoOuDesconhecido_Recusa(string codigo)
    {
        CatalogoDe<FormaDeComprovacao>.TentarObter(codigo, out _).Should().BeFalse();
        Action leitura = () => CatalogoDe<FormaDeComprovacao>.Obter(codigo);
        leitura.Should().Throw<ArgumentException>();
    }
}
