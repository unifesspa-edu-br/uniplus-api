namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ResultadoDaLiberacaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<ResultadoDaLiberacao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<ResultadoDaLiberacao, string>
            {
                [ResultadoDaLiberacao.Liberada] = "LIBERADA",
                [ResultadoDaLiberacao.Mantida] = "MANTIDA",
            });
}
