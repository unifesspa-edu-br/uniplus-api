namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class EtapaDoPedidoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<EtapaDoPedido>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<EtapaDoPedido, string>
            {
                [EtapaDoPedido.Gestor] = "GESTOR",
                [EtapaDoPedido.ProtecaoDeDados] = "PROTECAO_DE_DADOS",
            });
}
