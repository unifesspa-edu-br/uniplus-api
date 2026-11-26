namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class AtorDoEventoDoHistoricoDaConcessaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<AtorDoEventoDoHistoricoDaConcessao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(Esperados);

    private static readonly Dictionary<AtorDoEventoDoHistoricoDaConcessao, string> Esperados = new()
    {
        [AtorDoEventoDoHistoricoDaConcessao.Humano] = "HUMANO",
        [AtorDoEventoDoHistoricoDaConcessao.Sistema] = "SISTEMA",
    };
}
