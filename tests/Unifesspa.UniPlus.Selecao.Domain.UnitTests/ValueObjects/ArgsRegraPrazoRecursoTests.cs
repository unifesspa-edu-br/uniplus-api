namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// CA-22 (Story #851 §3.6 item 5): o prazo de interposição resolve do INSTANTE DE
/// PUBLICAÇÃO do ato âncora — nunca de data fixa. Função pura, sem I/O — o motor que a
/// executaria em runtime é incremento pós-#40 (§3.8); aqui só se prova que o valor
/// congelado é matematicamente correto e desliza com o ato.
/// </summary>
public sealed class ArgsRegraPrazoRecursoTests
{
    private static ArgsRegraPrazoRecurso Args(decimal prazoValor, UnidadePrazo unidade) => new(
        PrazoValor: prazoValor,
        PrazoUnidade: unidade,
        AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
        SuspensividadePrimeiraInstanciaValor: null,
        SuspensividadePrimeiraInstanciaUnidade: null,
        SuspensividadeSegundaInstanciaValor: null,
        SuspensividadeSegundaInstanciaUnidade: null);

    [Fact(DisplayName = "CA-22: prazo de 48 horas desliza com o instante de publicação do ato âncora")]
    public void Prazo_DeslizaComOAtoAncora()
    {
        ArgsRegraPrazoRecurso args = Args(48m, UnidadePrazo.Horas);

        DateTimeOffset publicacao1 = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset publicacao2 = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

        args.ResolverFimDaInterposicao(publicacao1).Should().Be(new DateTimeOffset(2026, 6, 14, 14, 0, 0, TimeSpan.Zero));
        args.ResolverFimDaInterposicao(publicacao2).Should().Be(new DateTimeOffset(2026, 6, 17, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Prazo em dias corridos soma dias corridos ao instante de publicação")]
    public void Prazo_EmDiasCorridos_SomaDias()
    {
        ArgsRegraPrazoRecurso args = Args(5m, UnidadePrazo.Dias);
        DateTimeOffset publicacao = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);

        args.ResolverFimDaInterposicao(publicacao).Should().Be(new DateTimeOffset(2026, 6, 17, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Prazo em DIAS_UTEIS não tem calendário para resolver — lança (o gate de publicação recusa antes de chegar aqui)")]
    public void Prazo_EmDiasUteis_Lanca()
    {
        ArgsRegraPrazoRecurso args = Args(3m, UnidadePrazo.DiasUteis);

        Action act = () => args.ResolverFimDaInterposicao(DateTimeOffset.UnixEpoch);

        act.Should().Throw<InvalidOperationException>();
    }
}
