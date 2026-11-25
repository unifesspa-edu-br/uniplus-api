namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ResultadoDoItemDaRevisaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<ResultadoDoItemDaRevisao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<ResultadoDoItemDaRevisao, string>
            {
                [ResultadoDoItemDaRevisao.Confirmada] = "CONFIRMADA",
                [ResultadoDoItemDaRevisao.Revogada] = "REVOGADA",
            });
}
