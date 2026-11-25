namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormaDoEncerramentoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<FormaDoEncerramento>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<FormaDoEncerramento, string>
            {
                [FormaDoEncerramento.Concluida] = "CONCLUIDA",
                [FormaDoEncerramento.Prazo] = "PRAZO",
                [FormaDoEncerramento.Manual] = "MANUAL",
            });
}
