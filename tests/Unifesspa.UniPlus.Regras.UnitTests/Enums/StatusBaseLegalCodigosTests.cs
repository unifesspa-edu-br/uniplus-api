namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class StatusBaseLegalCodigosTests
{
    [Fact]
    public void StatusBaseLegal_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<StatusBaseLegal>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<StatusBaseLegal, (string, string)>
            {
                [StatusBaseLegal.Pendente] = ("PENDENTE", "Pendente"),
                [StatusBaseLegal.Resolvido] = ("RESOLVIDO", "Resolvido"),
            });
    }

}
