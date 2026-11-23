namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class FonteDeMunicipiosDeProvaTests
{
    [Fact]
    public void Fonte_CodigoDeContrato_ConverteNosDoisSentidos()
    {
        FontesValoresFato.ParaTokenCanonico(FonteValoresFato.MunicipiosProvaPresencial)
            .Should().Be("MUNICIPIOS_PROVA_PRESENCIAL");
        FontesValoresFato.Analisar("MUNICIPIOS_PROVA_PRESENCIAL")
            .Should().Be(FonteValoresFato.MunicipiosProvaPresencial);
        FontesValoresFato.TokensCanonicos.Should().Contain("MUNICIPIOS_PROVA_PRESENCIAL");
    }
}
