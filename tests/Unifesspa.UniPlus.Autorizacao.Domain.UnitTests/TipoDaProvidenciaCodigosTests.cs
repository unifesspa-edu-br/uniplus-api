namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDaProvidenciaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDaProvidencia>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<TipoDaProvidencia, string>
            {
                [TipoDaProvidencia.Retirada] = "RETIRADA",
                [TipoDaProvidencia.Revisao] = "REVISAO",
            });
}
