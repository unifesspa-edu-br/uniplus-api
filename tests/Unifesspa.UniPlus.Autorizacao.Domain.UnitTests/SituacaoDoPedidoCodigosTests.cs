namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDoPedidoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDoPedido>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDoPedido, string>
            {
                [SituacaoDoPedido.AguardandoBeneficiario] = "AGUARDANDO_BENEFICIARIO",
                [SituacaoDoPedido.AguardandoGestor] = "AGUARDANDO_GESTOR",
                [SituacaoDoPedido.AguardandoProtecaoDeDados] = "AGUARDANDO_PROTECAO_DE_DADOS",
                [SituacaoDoPedido.Aprovado] = "APROVADO",
                [SituacaoDoPedido.Recusado] = "RECUSADO",
                [SituacaoDoPedido.Cancelado] = "CANCELADO",
                [SituacaoDoPedido.AguardandoDocumento] = "AGUARDANDO_DOCUMENTO",
                [SituacaoDoPedido.Devolvido] = "DEVOLVIDO",
            });
}
