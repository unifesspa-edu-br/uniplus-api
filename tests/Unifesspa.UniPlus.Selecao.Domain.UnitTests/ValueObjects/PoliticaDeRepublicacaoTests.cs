namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PoliticaDeRepublicacaoTests
{
    private static readonly TimeSpan SeisHoras = TimeSpan.FromHours(6);

    [Theory(DisplayName = "O prazo dobra a cada publicação até o teto")]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(10, 360)]
    public void PrazoApos_DobraAteOTeto(int publicacoes, int minutosEsperados) =>
        PoliticaDeRepublicacao.Padrao.PrazoApos(publicacoes).Should().Be(TimeSpan.FromMinutes(minutosEsperados));

    [Theory(DisplayName = "O prazo calculado sai em microssegundos inteiros, ainda que a base tenha resto de ticks")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void PrazoApos_BaseComResto_SaiEmMicrossegundosInteiros(int publicacoes)
    {
        PoliticaDeRepublicacao politica = new(TimeSpan.FromTicks(6_000_000_007), SeisHoras);

        TimeSpan prazo = politica.PrazoApos(publicacoes);

        (prazo.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
        prazo.Should().BeCloseTo(politica.Base * Math.Pow(2, publicacoes - 1), TimeSpan.FromTicks(10));
    }

    [Fact(DisplayName = "O teto configurado com resto de ticks também sai em microssegundos inteiros")]
    public void PrazoApos_TetoComResto_SaiEmMicrossegundosInteiros()
    {
        PoliticaDeRepublicacao politica = new(TimeSpan.FromMinutes(10), TimeSpan.FromTicks(216_000_000_007));

        TimeSpan prazo = politica.PrazoApos(30);

        prazo.Ticks.Should().Be(216_000_000_000);
    }

    [Fact(DisplayName = "Instante em microssegundos somado ao prazo calculado continua em microssegundos")]
    public void PrazoApos_SomadoAUmInstanteEmMicrossegundos_NaoReintroduzASetimaCasa()
    {
        DateTimeOffset agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        PoliticaDeRepublicacao politica = new(TimeSpan.FromTicks(6_000_000_001), SeisHoras);

        DateTimeOffset proxima = agora + politica.PrazoApos(1);

        (proxima.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }
}
