namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDoAnexoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDoAnexo>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDoAnexo, string>
            {
                [SituacaoDoAnexo.AguardandoEnvio] = "AGUARDANDO_ENVIO",
                [SituacaoDoAnexo.EmVerificacao] = "EM_VERIFICACAO",
                [SituacaoDoAnexo.Verificado] = "VERIFICADO",
                [SituacaoDoAnexo.Recusado] = "RECUSADO",
            });
}
