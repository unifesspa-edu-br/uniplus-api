namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class FonteDeMunicipiosDeProvaTests
{
    [Fact]
    public void Fonte_CodigoDeContrato_ConverteNosDoisSentidos()
    {
        FonteValoresFato.MunicipiosProvaPresencial.Codigo().Should().Be("MUNICIPIOS_PROVA_PRESENCIAL");
        CatalogoDe<FonteValoresFato>.Obter("MUNICIPIOS_PROVA_PRESENCIAL").Should().Be(FonteValoresFato.MunicipiosProvaPresencial);
        CatalogoDe<FonteValoresFato>.Todos().Select(static c => c.Codigo).Should().Contain("MUNICIPIOS_PROVA_PRESENCIAL");
    }
}
