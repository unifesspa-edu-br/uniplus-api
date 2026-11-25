namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class EstadoDaProvidenciaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<EstadoDaProvidencia>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<EstadoDaProvidencia, string>
            {
                [EstadoDaProvidencia.Aberta] = "ABERTA",
                [EstadoDaProvidencia.Respondida] = "RESPONDIDA",
                [EstadoDaProvidencia.Atendida] = "ATENDIDA",
            });
}
