namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class DateTimeOffsetExtensionsTests
{
    // Brasília deixou de ter horário de verão desde 2019 — offset constante -03:00.
    // Os testes fixam essa premissa: se a legislação voltar a mudar a tzdb a
    // assertiva quebra e o time fica sabendo que a extensão precisa ser
    // reavaliada (atualmente delega à tzdb do SO).
    private static readonly TimeSpan OffsetBrasilia = TimeSpan.FromHours(-3);

    [Fact(DisplayName = "ParaHorarioBrasilia converte UTC para offset -03:00 preservando o instante")]
    public void ConverteUtcParaBrasilia()
    {
        DateTimeOffset utc = new(2026, 5, 10, 18, 30, 0, TimeSpan.Zero);

        DateTimeOffset convertido = utc.ParaHorarioBrasilia();

        convertido.Offset.Should().Be(OffsetBrasilia);
        convertido.UtcDateTime.Should().Be(utc.UtcDateTime,
            "conversão de fuso não altera o instante absoluto");
        convertido.DateTime.Should().Be(new DateTime(2026, 5, 10, 15, 30, 0, DateTimeKind.Unspecified));
    }

    [Fact(DisplayName = "ParaHorarioBrasilia em data de janeiro (verão astronômico) mantém -03:00 (sem DST desde 2019)")]
    public void DataDeVerao_NaoAplicaDst()
    {
        DateTimeOffset utc = new(2026, 1, 15, 3, 0, 0, TimeSpan.Zero);

        DateTimeOffset convertido = utc.ParaHorarioBrasilia();

        convertido.Offset.Should().Be(OffsetBrasilia,
            "Brasil revogou horário de verão pelo Decreto 9.772/2019");
    }

    [Fact(DisplayName = "ParaHorarioBrasilia em DateTimeOffset já em offset distinto preserva o instante")]
    public void ValorEmOutroOffset_PreservaInstante()
    {
        DateTimeOffset emLisboa = new(2026, 5, 10, 21, 30, 0, TimeSpan.FromHours(1));

        DateTimeOffset convertido = emLisboa.ParaHorarioBrasilia();

        convertido.Offset.Should().Be(OffsetBrasilia);
        convertido.UtcDateTime.Should().Be(emLisboa.UtcDateTime);
        // 21:30 +01:00 = 20:30 UTC = 17:30 -03:00
        convertido.DateTime.Should().Be(new DateTime(2026, 5, 10, 17, 30, 0, DateTimeKind.Unspecified));
    }
}
