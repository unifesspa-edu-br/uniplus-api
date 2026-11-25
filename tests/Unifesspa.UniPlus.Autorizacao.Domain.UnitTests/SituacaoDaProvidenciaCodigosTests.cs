namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDaProvidenciaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDaProvidencia>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaProvidencia, string>
            {
                [SituacaoDaProvidencia.Aberta] = "ABERTA",
                [SituacaoDaProvidencia.Respondida] = "RESPONDIDA",
                [SituacaoDaProvidencia.Atendida] = "ATENDIDA",
                [SituacaoDaProvidencia.Prejudicada] = "PREJUDICADA",
            });
}
