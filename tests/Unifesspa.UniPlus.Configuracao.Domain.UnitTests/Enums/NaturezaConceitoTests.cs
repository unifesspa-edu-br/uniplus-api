namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Contrato do código e do rótulo que cada natureza de conceito declara.</summary>
public sealed class NaturezaConceitoTests
{
    [Fact(DisplayName = "O catálogo declara código e rótulo de cada natureza, na ordem do enum")]
    public void Catalogo_DeclaraCodigoERotuloDeCadaNatureza() =>
        CatalogoDe<NaturezaConceito>.Todos().Select(static d => (d.Codigo, d.Rotulo)).Should().Equal(
            ("NUMERICO", "Numérico"),
            ("CATEGORICO", "Categórico"));

    [Theory(DisplayName = "O código declarado volta à mesma natureza")]
    [InlineData("NUMERICO", NaturezaConceito.Numerico)]
    [InlineData("CATEGORICO", NaturezaConceito.Categorico)]
    public void TentarObter_CodigoDeclarado_DevolveANatureza(string codigo, NaturezaConceito esperada)
    {
        CatalogoDe<NaturezaConceito>.TentarObter(codigo, out NaturezaConceito natureza).Should().BeTrue();
        natureza.Should().Be(esperada);
    }

    [Theory(DisplayName = "Código desconhecido, na grafia antiga, em outra caixa, vazio ou nulo não é natureza")]
    [InlineData("ORDINAL")]
    [InlineData("numerico")]
    [InlineData("Categorico")]
    [InlineData("")]
    [InlineData(null)]
    public void TentarObter_CodigoDesconhecido_NaoDevolveNatureza(string? codigo) =>
        CatalogoDe<NaturezaConceito>.TentarObter(codigo, out _).Should().BeFalse();

    [Fact(DisplayName = "Código desconhecido lido do banco falha em vez de virar natureza")]
    public void Obter_CodigoDesconhecido_Falha() =>
        FluentActions.Invoking(static () => CatalogoDe<NaturezaConceito>.Obter("ORDINAL"))
            .Should().Throw<ArgumentException>();

    [Fact(DisplayName = "O enum passa na conferência dos códigos de contrato")]
    public void ConferirEnumsDe_DominioDaConfiguracao_Passa() =>
        FluentActions.Invoking(static () => EnumsDeContrato.ConferirEnumsDe([typeof(NaturezaConceito).Assembly]))
            .Should().NotThrow();
}
