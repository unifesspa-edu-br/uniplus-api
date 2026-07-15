namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Parsing por allowlist textual dos três eixos do catálogo de fatos (ADR-0111):
/// domínio, natureza (origem) e cardinalidade. Só os tokens canônicos UPPER_SNAKE
/// são aceitos; tokens PascalCase e fora do domínio (ex.: <c>TEXTO</c>) são
/// rejeitados — o que <c>Enum.TryParse</c> aceitaria por engano.
/// </summary>
public sealed class FatoCandidatoEnumsTests
{
    [Theory(DisplayName = "DominiosFato resolve os três tokens canônicos e o round-trip é estável")]
    [InlineData("CATEGORICO", DominioFato.Categorico)]
    [InlineData("BOOLEANO", DominioFato.Booleano)]
    [InlineData("NUMERICO", DominioFato.Numerico)]
    public void DominiosFato_TokenCanonico_Resolve(string token, DominioFato esperado)
    {
        DominiosFato.TryAnalisar(token, out DominioFato dominio).Should().BeTrue();
        dominio.Should().Be(esperado);
        DominiosFato.ParaTokenCanonico(dominio).Should().Be(token);
    }

    [Theory(DisplayName = "DominiosFato rejeita 'texto', PascalCase, vazio e nulo")]
    [InlineData("TEXTO")]
    [InlineData("Categorico")]
    [InlineData("categorico")]
    [InlineData("")]
    [InlineData(null)]
    public void DominiosFato_ForaDoDominio_Rejeita(string? token)
    {
        DominiosFato.TryAnalisar(token, out DominioFato dominio).Should().BeFalse();
        dominio.Should().Be(DominioFato.Nenhum);
        DominiosFato.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "DominiosFato expõe exatamente os três tokens canônicos")]
    public void DominiosFato_TokensCanonicos() =>
        DominiosFato.TokensCanonicos.Should().BeEquivalentTo(["CATEGORICO", "BOOLEANO", "NUMERICO"]);

    [Theory(DisplayName = "NaturezasFato resolve os três tokens de origem-do-dado")]
    [InlineData("BRUTO_INFORMADO", NaturezaFato.BrutoInformado)]
    [InlineData("DE_VONTADE", NaturezaFato.DeVontade)]
    [InlineData("DERIVADO", NaturezaFato.Derivado)]
    public void NaturezasFato_TokenCanonico_Resolve(string token, NaturezaFato esperada)
    {
        NaturezasFato.TryAnalisar(token, out NaturezaFato natureza).Should().BeTrue();
        natureza.Should().Be(esperada);
        NaturezasFato.ParaTokenCanonico(natureza).Should().Be(token);
    }

    [Theory(DisplayName = "NaturezasFato rejeita token fora do domínio e nulo")]
    [InlineData("ESTATICO")]
    [InlineData("DINAMICO")]
    [InlineData(null)]
    public void NaturezasFato_ForaDoDominio_Rejeita(string? token)
    {
        NaturezasFato.TryAnalisar(token, out NaturezaFato natureza).Should().BeFalse();
        natureza.Should().Be(NaturezaFato.Nenhuma);
    }

    [Theory(DisplayName = "CardinalidadesFato resolve os dois tokens canônicos")]
    [InlineData("ESCALAR", CardinalidadeFato.Escalar)]
    [InlineData("MULTIVALORADO", CardinalidadeFato.Multivalorado)]
    public void CardinalidadesFato_TokenCanonico_Resolve(string token, CardinalidadeFato esperada)
    {
        CardinalidadesFato.TryAnalisar(token, out CardinalidadeFato cardinalidade).Should().BeTrue();
        cardinalidade.Should().Be(esperada);
        CardinalidadesFato.ParaTokenCanonico(cardinalidade).Should().Be(token);
    }

    [Theory(DisplayName = "CardinalidadesFato rejeita token fora do domínio e nulo")]
    [InlineData("ARRAY")]
    [InlineData("Escalar")]
    [InlineData(null)]
    public void CardinalidadesFato_ForaDoDominio_Rejeita(string? token)
    {
        CardinalidadesFato.TryAnalisar(token, out CardinalidadeFato cardinalidade).Should().BeFalse();
        cardinalidade.Should().Be(CardinalidadeFato.Nenhuma);
    }
}
