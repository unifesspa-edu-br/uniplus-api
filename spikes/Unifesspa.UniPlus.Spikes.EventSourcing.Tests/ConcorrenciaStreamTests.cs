using AwesomeAssertions;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Escala horizontal: duas instâncias da API gravando no MESMO stream são, no nível
/// do banco, duas sessões concorrentes (a concorrência otimista do Marten é imposta
/// no PostgreSQL, independente do processo). Prova: o segundo writer leva a exceção
/// de concorrência de stream (<see cref="EventStreamUnexpectedMaxEventIdException"/>),
/// faz retry com estado fresco, e o estado final contém AS DUAS escritas — sem lost update.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class ConcorrenciaStreamTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "Escala: dois writers no mesmo stream → ConcurrencyException + retry sem lost update")]
    public async Task Dois_writers_no_mesmo_stream_concorrencia_otimista()
    {
        // Arrange: edital aberto e publicado (stream com 2 eventos).
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "040/2026", "Edital concorrência", TestHelpers.AtorFicticio()));
        await fixture.Bus.InvokeAsync(
            new PublicarEdital(editalId, "hash-config-v1", TestHelpers.AtorFicticio()));

        IDocumentStore store = fixture.Store;
        // Payload do ator é irrelevante para a projeção (que é PII-free).
        AtorCifrado ator = new(Guid.CreateVersion7(), Guid.CreateVersion7(), "irrelevante");

        // Act: duas sessões (= duas instâncias) carregam o MESMO stream na MESMA versão.
        await using IDocumentSession s1 = store.LightweightSession();
        await using IDocumentSession s2 = store.LightweightSession();
        IEventStream<EditalEs> w1 = await s1.Events.FetchForWriting<EditalEs>(editalId);
        IEventStream<EditalEs> w2 = await s2.Events.FetchForWriting<EditalEs>(editalId);

        w1.AppendOne(new EditalRetificado(editalId, "retificação A", ator, DateTimeOffset.UtcNow));
        w2.AppendOne(new EditalRetificado(editalId, "retificação B", ator, DateTimeOffset.UtcNow));

        // Writer 1 vence.
        await s1.SaveChangesAsync();

        // Writer 2 colide: a versão esperada do stream já avançou.
        Func<Task> colisao = () => s2.SaveChangesAsync();
        await colisao.Should().ThrowAsync<EventStreamUnexpectedMaxEventIdException>(
            "o segundo writer no mesmo stream deve detectar o conflito de versão");

        // Retry do writer 2 com estado fresco (re-fetch → nova versão).
        await using IDocumentSession s3 = store.LightweightSession();
        IEventStream<EditalEs> w3 = await s3.Events.FetchForWriting<EditalEs>(editalId);
        w3.AppendOne(new EditalRetificado(editalId, "retificação B (retry)", ator, DateTimeOffset.UtcNow));
        await s3.SaveChangesAsync();

        // Assert: nenhuma atualização perdida — AMBAS as retificações estão no agregado.
        await using IQuerySession consulta = store.QuerySession();
        EditalEs? view = await consulta.LoadAsync<EditalEs>(editalId);
        view!.QuantidadeRetificacoes.Should().Be(2,
            "writer 1 + retry do writer 2 — o conflito foi detectado e tratado, nada foi sobrescrito");

        // E o stream tem exatamente os 4 eventos esperados (Aberto, Publicado, 2× Retificado).
        IReadOnlyList<IEvent> eventos = await consulta.Events.FetchStreamAsync(editalId);
        eventos.Should().HaveCount(4, "o append perdedor não foi commitado; só os fatos válidos persistem");
    }
}
