namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

public sealed class LimiarDePendenciaEmMicrossegundosTests
{
    [Theory(DisplayName = "O limiar de pendência configurado com resto de ticks é guardado em microssegundos inteiros, sem arredondar")]
    [InlineData(18_000_000_001, 18_000_000_000)]
    [InlineData(18_000_000_009, 18_000_000_000)]
    [InlineData(18_000_000_010, 18_000_000_010)]
    public void LimiarDePendencia_ComResto_ChegaEmMicrossegundosInteiros(long ticks, long ticksEsperados)
    {
        AtosDoProcessoOptions opcoes = new() { LimiarDePendencia = TimeSpan.FromTicks(ticks) };

        opcoes.LimiarDePendencia.Ticks.Should().Be(ticksEsperados);
    }

    [Fact(DisplayName = "O limiar de pendência padrão é de meia hora")]
    public void LimiarDePendencia_Padrao_EMeiaHora() =>
        new AtosDoProcessoOptions().LimiarDePendencia.Should().Be(TimeSpan.FromMinutes(30));
}
