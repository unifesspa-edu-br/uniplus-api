namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class AtorDoEventoDaContencaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<AtorDoEventoDaContencao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<AtorDoEventoDaContencao, string>
            {
                [AtorDoEventoDaContencao.Humano] = "HUMANO",
                [AtorDoEventoDaContencao.Sistema] = "SISTEMA",
            });
}
