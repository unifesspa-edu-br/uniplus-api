using AwesomeAssertions;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G3 (LGPD): esquecer um titular (crypto-shredding) torna a PII do ator
/// irrecuperável, sem mutar o log append-only — o fato/stream permanece íntegro.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class CryptoShreddingTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "G3: antes de esquecer, a PII do ator é revelável; o evento existe")]
    public async Task Antes_de_esquecer_pii_e_revelavel()
    {
        Ator ator = new(Guid.CreateVersion7(), "Maria de Teste", "00000000272");
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(new AbrirEdital(editalId, "030/2026", "Edital G3", ator));

        AtorCifrado cifrado = await ObterAtorCifradoAsync(editalId);
        Ator? revelado = await RevelarAsync(cifrado);

        revelado.Should().NotBeNull();
        revelado!.Nome.Should().Be(ator.Nome);
        revelado.Cpf.Should().Be(ator.Cpf);
    }

    [Fact(DisplayName = "G3: após esquecer, a PII é irrecuperável mas o fato/stream permanece")]
    public async Task Apos_esquecer_pii_irrecuperavel_mas_fato_permanece()
    {
        Ator ator = new(Guid.CreateVersion7(), "João de Teste", "00000000353");
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(new AbrirEdital(editalId, "031/2026", "Edital G3 shred", ator));

        AtorCifrado cifrado = await ObterAtorCifradoAsync(editalId);
        (await RevelarAsync(cifrado)).Should().NotBeNull("a PII deve ser revelável antes do esquecimento");

        // Act: esquecer o titular (apaga as chaves; commit próprio)
        await fixture.Host.Services.GetRequiredService<IServicoEsquecimento>()
            .EsquecerAsync(ator.SujeitoId);

        // Assert: PII irrecuperável (retorna null, sem lançar)
        Ator? aposShred = await RevelarAsync(cifrado);
        aposShred.Should().BeNull("apagar a chave torna a PII indecifrável (crypto-shredding)");

        // Assert: o fato permanece — o stream segue íntegro com o evento de abertura
        await using IQuerySession session = fixture.Store.QuerySession();
        IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(editalId);
        eventos.Should().ContainSingle("o log append-only não é mutado pelo esquecimento");
        EditalEs? view = await session.LoadAsync<EditalEs>(editalId);
        view!.NumeroEdital.Should().Be("031/2026");
    }

    private async Task<AtorCifrado> ObterAtorCifradoAsync(Guid editalId)
    {
        await using IQuerySession session = fixture.Store.QuerySession();
        IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(editalId);
        return eventos.Select(e => e.Data).OfType<EditalAberto>().Single().Ator;
    }

    private Task<Ator?> RevelarAsync(AtorCifrado cifrado) =>
        fixture.Host.Services.GetRequiredService<IProtetorPii>().TentarRevelarAsync(cifrado);
}
