namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Interfaces;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A regra que classifica um certame divulgado numa das quatro situações da vitrine.
/// </summary>
/// <remarks>
/// O que estes testes protegem não é cada rótulo isolado, e sim a PARTIÇÃO: que todo certame caia
/// em exatamente uma situação, inclusive nos instantes exatos de borda. É dela que dependem a soma
/// dos contadores, a existência de um filtro para cada número exibido, e a concordância entre a
/// marca do item e o grupo em que ele foi listado.
/// </remarks>
public sealed class SituacaoDaVitrineTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limiar = TimeSpan.FromDays(7);

    [Theory(DisplayName = "Cada janela recebe a situação do ponto em que está")]
    [InlineData(5, 30, SituacaoDoCertame.EmBreve)]
    [InlineData(-1, 30, SituacaoDoCertame.InscricoesAbertas)]
    [InlineData(-1, 3, SituacaoDoCertame.UltimosDias)]
    [InlineData(-30, -1, SituacaoDoCertame.Encerradas)]
    public void Classificar_QuandoJanelaEmCadaPonto_DeveResolverASituacao(
        int diasAteAbrir, int diasAteFechar, SituacaoDoCertame esperada) =>
        SituacaoDaVitrine.Classificar(
            Agora.AddDays(diasAteAbrir), Agora.AddDays(diasAteFechar), Agora, Limiar)
            .Should().Be(esperada);

    [Fact(DisplayName = "No instante exato da abertura o certame já recebe inscrição")]
    public void Classificar_NaAberturaExata_NaoEEmBreve() =>
        // A janela é fechada nas duas pontas: quem já pode se inscrever não é "em breve".
        SituacaoDaVitrine.Classificar(Agora, Agora.AddDays(30), Agora, Limiar)
            .Should().Be(SituacaoDoCertame.InscricoesAbertas);

    [Fact(DisplayName = "No instante exato do encerramento o certame ainda recebe inscrição")]
    public void Classificar_NoEncerramentoExato_AindaNaoEncerrou() =>
        // Prazo que termina às 23h59 inclui as 23h59.
        SituacaoDaVitrine.Classificar(Agora.AddDays(-30), Agora, Agora, Limiar)
            .Should().Be(SituacaoDoCertame.UltimosDias);

    [Fact(DisplayName = "No instante exato do limiar o certame ainda não está nos últimos dias")]
    public void Classificar_NoLimiarExato_AindaEAberta() =>
        SituacaoDaVitrine.Classificar(Agora.AddDays(-1), Agora + Limiar, Agora, Limiar)
            .Should().Be(SituacaoDoCertame.InscricoesAbertas);

    [Fact(DisplayName = "Janela invertida cai numa situação só, em vez de em duas")]
    public void Classificar_JanelaInvertida_NaoDuplica() =>
        // O agregado não produz janela invertida, mas a partição não pode depender disso.
        SituacaoDaVitrine.Classificar(Agora.AddDays(5), Agora.AddDays(-5), Agora, Limiar)
            .Should().Be(SituacaoDoCertame.Encerradas);

    [Fact(DisplayName = "Toda janela possível recebe exatamente uma das quatro situações")]
    public void Classificar_QuandoVarreTodasAsBordas_DeveDevolverSempreUmaSituacaoValida()
    {
        // Produto das bordas de cada ponto que a regra usa: a função é total.
        int[] deslocamentos = [-30, -8, -7, -1, 0, 1, 3, 7, 8, 30];
        SituacaoDoCertame[] validas = Enum.GetValues<SituacaoDoCertame>();

        IEnumerable<SituacaoDoCertame> classificacoes =
            from de in deslocamentos
            from ate in deslocamentos
            select SituacaoDaVitrine.Classificar(Agora.AddDays(de), Agora.AddDays(ate), Agora, Limiar);

        classificacoes.Should().OnlyContain(situacao => validas.Contains(situacao));
    }

    [Fact(DisplayName = "O vocabulário não tem valor de \"sem filtro\"")]
    public void Vocabulario_QuandoEnumerado_NaoDeveTerValorDeAusenciaDeFiltro() =>
        // Ausência de recorte é ausência do parâmetro, não um valor que certame algum tenha.
        Enum.GetValues<SituacaoDoCertame>().Should().HaveCount(4);
}
