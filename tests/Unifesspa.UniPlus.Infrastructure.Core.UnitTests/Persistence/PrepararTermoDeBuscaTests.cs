namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class PrepararTermoDeBuscaTests
{
    [Theory(DisplayName = "Termo ausente, vazio, em branco ou só com caractere nulo não é busca")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    public void PrepararTermoDeBusca_SemConteudoPesquisavel_DevolveNulo(string? termo) =>
        NormalizacaoTextual.PrepararTermoDeBusca(termo).Should().BeNull();

    [Fact]
    public void PrepararTermoDeBusca_CaractereNuloNoMeio_DescartaOCaractere() =>
        NormalizacaoTextual.PrepararTermoDeBusca("a\0b").Should().Be("ab");

    [Fact]
    public void PrepararTermoDeBusca_TermoComAcentoEEspacos_AparaENormaliza() =>
        NormalizacaoTextual.PrepararTermoDeBusca("  Ação  ").Should().Be("acao");

    [Fact]
    public void PrepararTermoDeBusca_FormaDecomposta_ProduzOMesmoValorDaComposta() =>
        NormalizacaoTextual.PrepararTermoDeBusca("Ação")
            .Should().Be(NormalizacaoTextual.PrepararTermoDeBusca("Ação"))
            .And.Be("acao");

    [Fact]
    public void PrepararTermoDeBusca_CuringasDoLike_EscapaCadaUm() =>
        NormalizacaoTextual.PrepararTermoDeBusca("a%b_c\\d").Should().Be("a\\%b\\_c\\\\d");

    [Fact]
    public void PrepararTermoDeBusca_VazioEmBrancoEAusente_ProduzemOMesmoValor()
    {
        string?[] preparados =
        [
            NormalizacaoTextual.PrepararTermoDeBusca((string?)null),
            NormalizacaoTextual.PrepararTermoDeBusca(string.Empty),
            NormalizacaoTextual.PrepararTermoDeBusca("   "),
        ];

        preparados.Should().AllSatisfy(static preparado => preparado.Should().BeNull());
    }

    [Fact]
    public void PrepararTermoDeBusca_TermoValidadoAusente_DevolveNulo() =>
        NormalizacaoTextual.PrepararTermoDeBusca((TermoDeBusca?)null).Should().BeNull();

    [Fact]
    public void PrepararTermoDeBusca_TermoValidadoComCuringas_EscapaTextoNormalizado()
    {
        TermoDeBusca? termo = TermoDeBusca.Criar("  Ação%_\\  ").Value;

        NormalizacaoTextual.PrepararTermoDeBusca(termo).Should().Be("acao\\%\\_\\\\");
    }
}
