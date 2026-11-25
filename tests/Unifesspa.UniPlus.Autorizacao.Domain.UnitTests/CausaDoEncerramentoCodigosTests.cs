namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CausaDoEncerramentoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<CausaDoEncerramento>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<CausaDoEncerramento, string>
            {
                [CausaDoEncerramento.Revogada] = "REVOGADA",
                [CausaDoEncerramento.Vencida] = "VENCIDA",
            });
}
