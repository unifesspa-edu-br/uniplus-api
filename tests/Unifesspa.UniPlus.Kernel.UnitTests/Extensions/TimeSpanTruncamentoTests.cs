namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class TimeSpanTruncamentoTests
{
    [Theory(DisplayName = "TruncadoEmMicrossegundos descarta os ticks abaixo do microssegundo sem arredondar")]
    [InlineData(10_000_001, 10_000_000)]
    [InlineData(10_000_009, 10_000_000)]
    [InlineData(5, 0)]
    [InlineData(18_000_000_000_007, 18_000_000_000_000)]
    public void DescartaOsTicksResiduais(long ticks, long ticksEsperados)
    {
        TimeSpan truncada = TimeSpan.FromTicks(ticks).TruncadoEmMicrossegundos();

        truncada.Ticks.Should().Be(ticksEsperados);
    }

    [Theory(DisplayName = "TruncadoEmMicrossegundos de duração negativa também descarta o resíduo, em direção ao zero")]
    [InlineData(-10_000_001, -10_000_000)]
    [InlineData(-9, 0)]
    public void DuracaoNegativa_DescartaEmDirecaoAoZero(long ticks, long ticksEsperados) =>
        TimeSpan.FromTicks(ticks).TruncadoEmMicrossegundos().Ticks.Should().Be(ticksEsperados);

    [Fact(DisplayName = "TruncadoEmMicrossegundos mantém a duração já em microssegundos inteiros")]
    public void DuracaoJaEmMicrossegundos_PermaneceIgual()
    {
        TimeSpan quinzeMinutos = TimeSpan.FromMinutes(15);

        quinzeMinutos.TruncadoEmMicrossegundos().Should().Be(quinzeMinutos);
    }

    [Fact(DisplayName = "Instante em microssegundos somado a duração truncada continua múltiplo de 10 ticks")]
    public void SomaComInstanteEmMicrossegundos_NaoReintroduzASetimaCasa()
    {
        DateTimeOffset instante = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        TimeSpan duracaoComResto = TimeSpan.FromTicks(10_000_001);

        DateTimeOffset soma = instante + duracaoComResto.TruncadoEmMicrossegundos();

        (soma.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }

    [Fact(DisplayName = "TruncadoEmMicrossegundos mantém TimeSpan.MaxValue múltiplo de 10 ticks")]
    public void ValorMaximo_DescartaOResto() =>
        (TimeSpan.MaxValue.TruncadoEmMicrossegundos().Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
}
