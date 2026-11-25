namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDaInstalacaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDaInstalacao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaInstalacao, string>
            {
                [SituacaoDaInstalacao.AguardandoDocumento] = "AGUARDANDO_DOCUMENTO",
                [SituacaoDaInstalacao.DocumentoEmVerificacao] = "DOCUMENTO_EM_VERIFICACAO",
                [SituacaoDaInstalacao.DocumentoVerificado] = "DOCUMENTO_VERIFICADO",
                [SituacaoDaInstalacao.DocumentoRecusado] = "DOCUMENTO_RECUSADO",
                [SituacaoDaInstalacao.AguardandoCodigo] = "AGUARDANDO_CODIGO",
                [SituacaoDaInstalacao.Confirmada] = "CONFIRMADA",
                [SituacaoDaInstalacao.Cancelada] = "CANCELADA",
                [SituacaoDaInstalacao.Expirada] = "EXPIRADA",
            });
}
