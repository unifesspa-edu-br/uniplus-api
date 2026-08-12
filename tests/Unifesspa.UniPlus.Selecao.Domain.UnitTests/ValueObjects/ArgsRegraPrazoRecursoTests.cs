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

        args.ResolverFimDaInterposicao(publicacao1, diasNaoUteis: []).Should().Be(new DateTimeOffset(2026, 6, 14, 14, 0, 0, TimeSpan.Zero));
        args.ResolverFimDaInterposicao(publicacao2, diasNaoUteis: []).Should().Be(new DateTimeOffset(2026, 6, 17, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Prazo em dias corridos soma dias corridos ao instante de publicação")]
    public void Prazo_EmDiasCorridos_SomaDias()
    {
        ArgsRegraPrazoRecurso args = Args(5m, UnidadePrazo.Dias);
        DateTimeOffset publicacao = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);

        args.ResolverFimDaInterposicao(publicacao, diasNaoUteis: []).Should().Be(new DateTimeOffset(2026, 6, 17, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "CA-04: prazo em DIAS_UTEIS pula fins de semana E os dias não úteis recebidos como argumento")]
    public void Prazo_EmDiasUteis_PulaFimDeSemanaEDiasNaoUteis()
    {
        // Sexta-feira 2026-06-12. 3 dias úteis: sáb/dom (12/13-14) pulam sempre; a
        // segunda 15/06 é feriado (diasNaoUteis) — também pula; terça 16, quarta 17 e
        // quinta 18 contam. Resultado: quinta 18/06.
        ArgsRegraPrazoRecurso args = Args(3m, UnidadePrazo.DiasUteis);
        DateTimeOffset publicacao = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);
        DateOnly[] diasNaoUteis = [new DateOnly(2026, 6, 15)];

        DateTimeOffset resultado = args.ResolverFimDaInterposicao(publicacao, diasNaoUteis);

        resultado.Should().Be(new DateTimeOffset(2026, 6, 18, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "CA-05 (nível puro): a MESMA magnitude em DIAS_UTEIS produz resultado diferente conforme a data está ou não em diasNaoUteis — a distinção que sustenta a regra de abrangência")]
    public void Prazo_EmDiasUteis_DiscriminaPresencaDeDiaNaoUtil()
    {
        ArgsRegraPrazoRecurso args = Args(1m, UnidadePrazo.DiasUteis);
        // Sexta-feira 2026-06-12: 1 dia útil sem nenhum feriado no meio cai na
        // segunda-feira seguinte (pula sáb/dom).
        DateTimeOffset publicacao = new(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);

        DateTimeOffset semFeriado = args.ResolverFimDaInterposicao(publicacao, diasNaoUteis: []);
        DateTimeOffset comFeriadoNaSegunda = args.ResolverFimDaInterposicao(publicacao, diasNaoUteis: [new DateOnly(2026, 6, 15)]);

        semFeriado.Should().Be(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero));
        comFeriadoNaSegunda.Should().Be(new DateTimeOffset(2026, 6, 16, 14, 0, 0, TimeSpan.Zero));
        comFeriadoNaSegunda.Should().NotBe(semFeriado, "a presença do dia em diasNaoUteis muda o resultado — é essa distinção que a filtragem por localidade (Application) precisa produzir corretamente");
    }

    [Fact(DisplayName = "Convenção de data civil é UTC — instante perto da virada do dia em fuso não-zero é comparado pela data em UTC, não pela data local")]
    public void Prazo_EmDiasUteis_ConvencaoDeDataCivilEUtc()
    {
        // 2026-06-12T23:30:00-03:00 é 2026-06-13T02:30:00 UTC — dia civil UTC já é
        // sábado (13/06). O próximo dia útil (pulando sáb/dom 13-14) é a segunda 15/06.
        ArgsRegraPrazoRecurso args = Args(1m, UnidadePrazo.DiasUteis);
        DateTimeOffset publicacaoOffsetNaoZero = new(2026, 6, 12, 23, 30, 0, TimeSpan.FromHours(-3));

        DateTimeOffset resultado = args.ResolverFimDaInterposicao(publicacaoOffsetNaoZero, diasNaoUteis: []);

        resultado.UtcDateTime.Date.Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact(DisplayName = "A MESMA data civil UTC governa fim de semana E a busca em diasNaoUteis — não apenas uma das duas checagens")]
    public void Prazo_EmDiasUteis_ConvencaoDeDataCivilEUtcTambemParaDiasNaoUteis()
    {
        // Retoma o instante do teste acima (13/06 e 14/06 em UTC são sáb/dom, pulados por
        // QUALQUER convenção — não discriminam nada sozinhos). 15/06 (segunda, UTC) é o
        // primeiro candidato onde a busca em diasNaoUteis de fato roda. Sob o offset
        // -03:00, a data LOCAL desse mesmo instante é 14/06 — se a busca em diasNaoUteis
        // usasse essa data local em vez da UTC, um feriado cadastrado em 15/06 não seria
        // encontrado, e o resultado pararia incorretamente em 15/06 em vez de avançar
        // para 16/06 (terça, UTC).
        ArgsRegraPrazoRecurso args = Args(1m, UnidadePrazo.DiasUteis);
        DateTimeOffset publicacaoOffsetNaoZero = new(2026, 6, 12, 23, 30, 0, TimeSpan.FromHours(-3));

        DateTimeOffset resultado = args.ResolverFimDaInterposicao(
            publicacaoOffsetNaoZero, diasNaoUteis: [new DateOnly(2026, 6, 15)]);

        resultado.UtcDateTime.Date.Should().Be(new DateTime(2026, 6, 16));
    }
}
