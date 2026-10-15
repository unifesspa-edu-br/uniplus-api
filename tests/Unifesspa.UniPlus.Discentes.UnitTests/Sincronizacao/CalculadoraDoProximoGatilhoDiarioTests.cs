namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

public sealed class CalculadoraDoProximoGatilhoDiarioTests
{
    private static readonly TimeZoneInfo FusoBelem = TimeZoneInfo.FindSystemTimeZoneById("America/Belem");

    [Fact]
    public void Calcula_a_proxima_ocorrencia_no_fuso_configurado()
    {
        DateTimeOffset agora = new(2026, 10, 8, 14, 0, 0, TimeSpan.Zero);

        DateTimeOffset proxima = CalculadoraDoProximoGatilhoDiario.Calcular(
            agora,
            new TimeOnly(3, 0),
            FusoBelem);

        proxima.Should().Be(new DateTimeOffset(2026, 10, 9, 6, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Nao_agenda_novamente_a_data_ja_reservada()
    {
        DateTimeOffset agora = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

        DateTimeOffset proxima = CalculadoraDoProximoGatilhoDiario.Calcular(
            agora,
            new TimeOnly(3, 0),
            FusoBelem,
            new DateOnly(2026, 10, 8));

        proxima.Should().Be(new DateTimeOffset(2026, 10, 10, 6, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Avanca_para_o_primeiro_horario_valido_ao_entrar_no_horario_de_verao()
    {
        TimeZoneInfo fusoNovaYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        DateTimeOffset antesDaTransicao = new(2026, 3, 8, 5, 0, 0, TimeSpan.Zero);

        DateTimeOffset proxima = CalculadoraDoProximoGatilhoDiario.Calcular(
            antesDaTransicao,
            new TimeOnly(2, 30),
            fusoNovaYork);

        proxima.Should().Be(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Escolhe_uma_unica_ocorrencia_no_horario_ambiguo_de_retorno_do_verao()
    {
        TimeZoneInfo fusoNovaYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        DateTimeOffset antesDaRepeticao = new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);

        DateTimeOffset proxima = CalculadoraDoProximoGatilhoDiario.Calcular(
            antesDaRepeticao,
            new TimeOnly(1, 30),
            fusoNovaYork);

        proxima.Should().Be(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
    }
}
