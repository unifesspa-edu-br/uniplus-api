namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public sealed class BaseLegalBonusRegionalMappingTests
{
    [Fact(DisplayName = "A resposta da base legal expõe o tipo de instrumento com código e rótulo")]
    public void ToDto_ExpoeOTipoDeInstrumentoComCodigoERotulo()
    {
        BaseLegalBonusRegional baseLegal = BaseLegalBonusRegional.Criar(
            "INSTRUCAO_NORMATIVA", "Instrução Normativa 5/2022", "Institui o bônus", [("1504208", "Marabá", "PA")]).Value!;

        BaseLegalBonusRegionalDto dto = baseLegal.ToDto();

        dto.TipoInstrumento.Codigo.Should().Be("INSTRUCAO_NORMATIVA");
        dto.TipoInstrumento.Rotulo.Should().Be("Instrução Normativa");
    }
}
