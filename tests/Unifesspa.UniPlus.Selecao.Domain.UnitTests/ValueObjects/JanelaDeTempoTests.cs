namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A janela de tempo: extremos em UTC, completude, inversão e a posição de um instante nela.</summary>
public sealed class JanelaDeTempoTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Encerramento = Abertura.AddDays(10);

    [Fact(DisplayName = "Os extremos são guardados em UTC, qualquer que seja o offset com que chegaram")]
    public void Construir_ComOffset_GuardaEmUtc()
    {
        JanelaDeTempo janela = new(
            new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.FromHours(-3)));

        janela.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
        janela.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
        janela.Inicio.Should().Be(Abertura);
        janela.Fim.Should().Be(Encerramento);
    }

    [Fact(DisplayName = "Janelas com os mesmos instantes em offsets diferentes são iguais")]
    public void Igualdade_MesmosInstantesEmOffsetsDiferentes_SaoIguais()
    {
        JanelaDeTempo emUtc = new(Abertura, Encerramento);
        JanelaDeTempo emBelem = new(Abertura.ToOffset(TimeSpan.FromHours(-3)), Encerramento.ToOffset(TimeSpan.FromHours(-3)));

        emBelem.Should().Be(emUtc);
    }

    [Theory(DisplayName = "A janela só é completa com os dois extremos")]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Completa_DependeDosDoisExtremos(bool temInicio, bool temFim, bool esperada)
    {
        JanelaDeTempo janela = new(temInicio ? Abertura : null, temFim ? Encerramento : null);

        janela.Completa.Should().Be(esperada);
    }

    [Fact(DisplayName = "A janela com o fim antes do início está invertida")]
    public void Invertida_FimAntesDoInicio_EVerdadeira()
    {
        new JanelaDeTempo(Encerramento, Abertura).Invertida.Should().BeTrue();
    }

    [Fact(DisplayName = "Extremos no mesmo instante não invertem a janela")]
    public void Invertida_ExtremosIguais_EFalsa()
    {
        new JanelaDeTempo(Abertura, Abertura).Invertida.Should().BeFalse();
    }

    [Fact(DisplayName = "A janela incompleta nunca está invertida")]
    public void Invertida_JanelaIncompleta_EFalsa()
    {
        new JanelaDeTempo(Encerramento, null).Invertida.Should().BeFalse();
        new JanelaDeTempo(null, Abertura).Invertida.Should().BeFalse();
    }

    [Theory(DisplayName = "O extremo ausente é o primeiro que falta, e nulo na janela completa")]
    [InlineData(false, false, "inicio")]
    [InlineData(false, true, "inicio")]
    [InlineData(true, false, "fim")]
    [InlineData(true, true, null)]
    public void ExtremoAusente_IndicaOPrimeiroQueFalta(bool temInicio, bool temFim, string? esperado)
    {
        JanelaDeTempo janela = new(temInicio ? Abertura : null, temFim ? Encerramento : null);

        janela.ExtremoAusente.Should().Be(esperado);
    }

    [Theory(DisplayName = "A posição do instante é estrita nos extremos: o extremo em si está dentro")]
    [InlineData(-1, PosicaoNaJanela.Antes)]
    [InlineData(0, PosicaoNaJanela.Dentro)]
    [InlineData(5, PosicaoNaJanela.Dentro)]
    [InlineData(10, PosicaoNaJanela.Dentro)]
    [InlineData(11, PosicaoNaJanela.Depois)]
    public void PosicaoDe_JanelaCompleta_ComparaPorInstante(int diasDepoisDaAbertura, PosicaoNaJanela esperada)
    {
        JanelaDeTempo janela = new(Abertura, Encerramento);

        janela.PosicaoDe(Abertura.AddDays(diasDepoisDaAbertura)).Should().Be(esperada);
    }

    [Fact(DisplayName = "A posição compara por instante, e não pelo offset com que o instante chegou")]
    public void PosicaoDe_InstanteEmOutroOffset_ComparaPorInstante()
    {
        JanelaDeTempo janela = new(Abertura, Encerramento);
        DateTimeOffset aberturaEmBelem = Abertura.ToOffset(TimeSpan.FromHours(-3));

        janela.PosicaoDe(aberturaEmBelem).Should().Be(PosicaoNaJanela.Dentro);
        janela.PosicaoDe(aberturaEmBelem.AddTicks(-1)).Should().Be(PosicaoNaJanela.Antes);
    }

    [Fact(DisplayName = "O extremo ausente não limita a posição")]
    public void PosicaoDe_ExtremoAusente_NaoLimita()
    {
        new JanelaDeTempo(Abertura, null).PosicaoDe(Abertura.AddYears(5)).Should().Be(PosicaoNaJanela.Dentro);
        new JanelaDeTempo(null, Encerramento).PosicaoDe(Encerramento.AddYears(-5)).Should().Be(PosicaoNaJanela.Dentro);
        new JanelaDeTempo(null, null).PosicaoDe(Abertura).Should().Be(PosicaoNaJanela.Dentro);
    }
}
