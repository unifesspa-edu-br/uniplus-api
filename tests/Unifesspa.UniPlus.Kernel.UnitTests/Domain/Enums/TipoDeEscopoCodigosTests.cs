namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class TipoDeEscopoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDeEscopo>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<TipoDeEscopo, string>
            {
                [TipoDeEscopo.Unidade] = "UNIDADE",
                [TipoDeEscopo.Instituicao] = "INSTITUICAO",
            });

    [Fact]
    public void Rotulo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDeEscopo>().ToDictionary(static valor => valor, static valor => valor.Rotulo())
            .Should().Equal(new Dictionary<TipoDeEscopo, string>
            {
                [TipoDeEscopo.Unidade] = "Unidade",
                [TipoDeEscopo.Instituicao] = "Instituição",
            });
}
