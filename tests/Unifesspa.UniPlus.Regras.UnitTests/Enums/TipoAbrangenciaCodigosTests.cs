namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoAbrangenciaCodigosTests
{
    [Fact]
    public void TipoAbrangencia_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<TipoAbrangencia>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<TipoAbrangencia, (string, string)>
            {
                [TipoAbrangencia.Federal] = ("FEDERAL", "Federal"),
                [TipoAbrangencia.Estadual] = ("ESTADUAL", "Estadual"),
                [TipoAbrangencia.Municipal] = ("MUNICIPAL", "Municipal"),
                [TipoAbrangencia.InternaNorma] = ("INTERNA_NORMA", "Norma interna"),
                [TipoAbrangencia.InternaEdital] = ("INTERNA_EDITAL", "Edital"),
            });
    }

}
