namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class OrigemDoAvisoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<OrigemDoAviso>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<OrigemDoAviso, string>
            {
                [OrigemDoAviso.Reclassificacao] = "RECLASSIFICACAO",
                [OrigemDoAviso.Divergencia] = "DIVERGENCIA",
            });
}
