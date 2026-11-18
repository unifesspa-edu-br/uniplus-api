namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O instante do envelope do congelamento e do hash do comprovante (ADR-0100 item 3): UTC, RFC 3339, seis
/// casas fixas de fração de segundo e sufixo <c>Z</c>, com um ponto único de escrita e de leitura.
/// </summary>
public sealed class InstanteCanonicoTests
{
    [Theory(DisplayName = "O instante é escrito com seis casas fixas de fração, mesmo sem fração")]
    [InlineData(0, "2026-07-08T10:30:45.000000Z")]
    [InlineData(1234560, "2026-07-08T10:30:45.123456Z")]
    [InlineData(9999990, "2026-07-08T10:30:45.999999Z")]
    public void Serializar_EscreveSeisCasasFixas(long ticksDaFracao, string esperado)
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 7, 8, 10, 30, 45, TimeSpan.Zero).AddTicks(ticksDaFracao);

        HashCanonicalComputer.SerializeInstantCanonical(instante).Should().Be(esperado);
    }

    [Fact(DisplayName = "A sétima casa da fração é truncada, não arredondada para a casa anterior")]
    public void Serializar_TruncaASetimaCasa()
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 7, 8, 10, 30, 45, TimeSpan.Zero).AddTicks(1234567);

        HashCanonicalComputer.SerializeInstantCanonical(instante).Should().Be("2026-07-08T10:30:45.123456Z");
    }

    [Fact(DisplayName = "O instante com deslocamento é escrito em UTC")]
    public void Serializar_ConverteODeslocamentoParaUtc()
    {
        DateTimeOffset instante = new(2026, 7, 8, 7, 30, 45, TimeSpan.FromHours(-3));

        HashCanonicalComputer.SerializeInstantCanonical(instante).Should().Be("2026-07-08T10:30:45.000000Z");
    }

    [Fact(DisplayName = "Escrever e ler um instante com microssegundos devolve o mesmo instante")]
    public void IdaEVolta_PreservaOsMicrossegundos()
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 7, 8, 10, 30, 45, TimeSpan.Zero).AddTicks(1234560);

        bool lido = HashCanonicalComputer.TryParseInstantCanonical(HashCanonicalComputer.SerializeInstantCanonical(instante), out DateTimeOffset volta);

        lido.Should().BeTrue();
        volta.Should().Be(instante);
        volta.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory(DisplayName = "O instante fora da forma de seis casas e Z é recusado na leitura")]
    [InlineData("2026-07-08T10:30:45Z")]
    [InlineData("2026-07-08T10:30:45.123Z")]
    [InlineData("2026-07-08T10:30:45.1234567Z")]
    [InlineData("2026-07-08T10:30:45.123456+00:00")]
    [InlineData("2026-07-08T10:30:45.123456")]
    [InlineData("2026-07-08T10:30:45.123456z")]
    [InlineData("2026-07-08 10:30:45.123456Z")]
    [InlineData("")]
    [InlineData(null)]
    public void Ler_RecusaOutraForma(string? texto)
    {
        HashCanonicalComputer.TryParseInstantCanonical(texto, out _).Should().BeFalse();
    }
}
