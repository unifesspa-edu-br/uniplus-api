namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class DateTimeOffsetTruncamentoTests
{
    private static readonly DateTimeOffset BaseEmBrasilia = new(2026, 11, 13, 9, 30, 15, TimeSpan.FromHours(-3));

    [Theory(DisplayName = "TruncadoEmMicrossegundos descarta a 7ª casa sem arredondar")]
    [InlineData(1_234_567, 1_234_560)]
    [InlineData(9_999_999, 9_999_990)]
    [InlineData(5, 0)]
    public void DescartaSetimaCasaSemArredondar(long ticksAdicionais, long ticksEsperados)
    {
        DateTimeOffset truncado = BaseEmBrasilia.AddTicks(ticksAdicionais).TruncadoEmMicrossegundos();

        truncado.Should().Be(BaseEmBrasilia.AddTicks(ticksEsperados));
    }

    [Fact(DisplayName = "TruncadoEmMicrossegundos preserva o offset diferente de zero")]
    public void PreservaOffset()
    {
        DateTimeOffset truncado = BaseEmBrasilia.AddTicks(7).TruncadoEmMicrossegundos();

        truncado.Offset.Should().Be(TimeSpan.FromHours(-3));
        truncado.Ticks.Should().Be(BaseEmBrasilia.Ticks);
    }

    [Fact(DisplayName = "TruncadoEmMicrossegundos é idempotente em valor já truncado")]
    public void ValorJaTruncado_PermaneceIgual()
    {
        DateTimeOffset truncado = BaseEmBrasilia.AddTicks(1_234_560);

        truncado.TruncadoEmMicrossegundos().Should().Be(truncado);
        truncado.TruncadoEmMicrossegundos().Ticks.Should().Be(truncado.Ticks);
    }

    [Fact(DisplayName = "TruncadoEmMicrossegundos mantém DateTimeOffset.MinValue")]
    public void ValorMinimo_PermaneceIgual()
    {
        DateTimeOffset.MinValue.TruncadoEmMicrossegundos().Should().Be(DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "TruncadoEmMicrossegundos de DateTimeOffset.MaxValue descarta só o último tick")]
    public void ValorMaximo_DescartaUltimoTick()
    {
        DateTimeOffset truncado = DateTimeOffset.MaxValue.TruncadoEmMicrossegundos();

        truncado.Ticks.Should().Be(DateTimeOffset.MaxValue.Ticks - 9);
        (truncado.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }
}
