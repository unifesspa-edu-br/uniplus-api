namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Espera única pelo registro de um <see cref="AtoNormativo"/> em Publicações — a fila que
/// entrega a requisição do ato (ADR-0108) é assíncrona, e o <c>204</c> da publicação/retificação
/// volta muito antes de o ato existir. Consolida as duas cópias que existiam antes desta prova de
/// ponta a ponta (uma com orçamento de 30s, outra com 20s — ambas devolviam <see langword="null"/>
/// no timeout e empurravam o diagnóstico para uma asserção de nulidade posterior, sem dizer o que
/// se esperava).
/// </summary>
internal static class EsperaDeAtoRegistrado
{
    private static readonly TimeSpan OrcamentoPadrao = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IntervaloEntrePolls = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Aguarda o <see cref="AtoNormativo"/> de id <paramref name="atoId"/> aparecer em
    /// Publicações. Falha o teste <b>dentro</b> da espera (via AwesomeAssertions) quando o
    /// orçamento — independente por chamada, nunca compartilhado entre atos de um mesmo cenário —
    /// esgota: nunca devolve <see langword="null"/> silenciosamente para uma asserção posterior
    /// decidir. A mensagem nomeia o ato esperado, o processo, a versão que o congelou (quando
    /// informada), o ato predecessor (quando é uma retificação) e o tempo decorrido.
    /// </summary>
    /// <param name="api">A factory da coleção — cada chamada abre o próprio escopo de DI, nunca reaproveita um <see cref="Microsoft.EntityFrameworkCore.DbContext"/> tracked de outra consulta.</param>
    /// <param name="atoId">O id do ato — decidido por Seleção, nunca por Publicações (é o que torna a reentrega idempotente).</param>
    /// <param name="checkpoint">Descrição curta do passo do cenário (ex.: "ato V1 da publicação", "ato V2 do fechamento") — entra literal na mensagem de falha.</param>
    /// <param name="processoId">O processo ao qual o ato pertence — só para diagnóstico.</param>
    /// <param name="atoPredecessorId">Quando o ato esperado é uma retificação, o ato que ele emenda — só para diagnóstico.</param>
    /// <param name="orcamento">Tempo máximo de espera; <see cref="OrcamentoPadrao"/> (30s) quando omitido.</param>
    public static async Task<AtoNormativo> AguardarAsync(
        CascadingApiFactory api,
        Guid atoId,
        string checkpoint,
        Guid processoId,
        Guid? atoPredecessorId = null,
        TimeSpan? orcamento = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(checkpoint);

        TimeSpan budget = orcamento ?? OrcamentoPadrao;
        long inicio = Stopwatch.GetTimestamp();

        while (true)
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
            AtoNormativo? ato = await db.Set<AtoNormativo>().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == atoId, cancellationToken)
                .ConfigureAwait(false);
            if (ato is not null)
            {
                return ato;
            }

            TimeSpan decorrido = Stopwatch.GetElapsedTime(inicio);
            if (decorrido >= budget)
            {
                string predecessor = atoPredecessorId is { } predecessorId
                    ? $", predecessor {predecessorId}"
                    : string.Empty;

                // A falha acontece AQUI, dentro da espera — quem lê o teste vermelho vê de
                // imediato qual ato faltou, de qual processo e há quanto tempo, em vez de um
                // NullReferenceException genérico numa linha posterior. Falha direta em vez de
                // asserção sobre `ato`: neste ponto ele é necessariamente nulo (o retorno acima
                // trata o caso encontrado), e afirmar não-nulidade de algo sabidamente nulo
                // esconde a intenção — o que se quer é encerrar a espera com o diagnóstico.
                Assert.Fail(
                    $"{checkpoint}: o ato {atoId} do processo {processoId}{predecessor} não foi " +
                    $"observado em Publicações após {decorrido.TotalSeconds:F1}s — nenhum " +
                    "AtoNormativo com esse id existe na tabela. A publicação/retificação já " +
                    "respondeu antes desta espera começar; o registro chega pela fila durável " +
                    "do outbox (ADR-0108) e pode não ter drenado a tempo, ou foi recusado no " +
                    "consumo.");
            }

            await Task.Delay(IntervaloEntrePolls, cancellationToken).ConfigureAwait(false);
        }
    }
}
