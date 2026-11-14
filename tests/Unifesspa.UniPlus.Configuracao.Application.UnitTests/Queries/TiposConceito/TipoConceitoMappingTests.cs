namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries.TiposConceito;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

public sealed class TipoConceitoMappingTests
{
    [Fact(DisplayName = "O DTO do tipo numérico traz a escala e nenhum valor categórico")]
    public void ToDto_Numerico_TrazAEscala()
    {
        TipoConceitoDto dto = TiposConceitoDeTeste.Numerico().ToDto();

        dto.Natureza.Should().Be("numerico");
        dto.Numerico.Should().Be(new EscalaNumericaDto(0m, 10m, 2));
        dto.Valores.Should().BeNull();
    }

    [Fact(DisplayName = "O DTO do tipo categórico traz os valores na ordem e nenhuma escala")]
    public void ToDto_Categorico_TrazOsValoresEmOrdem()
    {
        TipoConceitoDto dto = TiposConceitoDeTeste.Categorico().ToDto();

        dto.Natureza.Should().Be("categorico");
        dto.Numerico.Should().BeNull();
        dto.Valores.Should().Equal(new ValorCategoricoDto("DEFERIDO", "Deferido", true), new ValorCategoricoDto("INDEFERIDO", "Indeferido", false));
    }

    [Fact(DisplayName = "O DTO expõe o código, o nome e a situação do tipo")]
    public void ToDto_TrazAIdentificacaoEASituacao()
    {
        TipoConceitoDto dto = TiposConceitoDeTeste.Numerico().ToDto();

        (dto.Codigo, dto.Nome, dto.Ativo).Should().Be(("NOTA_ENTREVISTA", "Nota da entrevista", true));
    }
}
