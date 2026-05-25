using AwesomeAssertions;
using JasperFx;
using JasperFx.Events;
using Marten;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>Evento legado (v1) — escrito por "código antigo".</summary>
public sealed record PrazoDefinidoV1(Guid EditalId, int Dias);

/// <summary>Evento evoluído (v2) — adiciona <c>Origem</c>.</summary>
public sealed record PrazoDefinidoV2(Guid EditalId, int Dias, string Origem);

/// <summary>
/// Gate G5: versionamento/upcasting de eventos. Um evento v1 persistido por código
/// antigo é lido como v2 por código novo, via upcaster — sem reescrever o passado.
/// Usa um store isolado (schema próprio) no mesmo PostgreSQL da fixture.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class UpcastingTests(SpikeFixture fixture)
{
    private const string Schema = "upcast_demo";

    [Fact(DisplayName = "G5: evento v1 legado é lido como v2 via upcaster")]
    public async Task Evento_v1_legado_e_lido_como_v2()
    {
        Guid streamId = Guid.CreateVersion7();

        // "Código antigo": só conhece v1, escreve o evento legado.
        await using (DocumentStore antigo = DocumentStore.For(o =>
        {
            o.Connection(fixture.ConnectionString);
            o.DatabaseSchemaName = Schema;
            o.AutoCreateSchemaObjects = AutoCreate.All;
        }))
        {
            await using IDocumentSession session = antigo.LightweightSession();
            session.Events.StartStream(streamId, new PrazoDefinidoV1(streamId, 30));
            await session.SaveChangesAsync();
        }

        // "Código novo": registra upcaster v1 → v2 e lê o stream.
        await using (DocumentStore novo = DocumentStore.For(o =>
        {
            o.Connection(fixture.ConnectionString);
            o.DatabaseSchemaName = Schema;
            o.AutoCreateSchemaObjects = AutoCreate.All;
            o.Events.Upcast<PrazoDefinidoV1, PrazoDefinidoV2>(
                v1 => new PrazoDefinidoV2(v1.EditalId, v1.Dias, "upcast-de-v1"));
        }))
        {
            await using IQuerySession session = novo.QuerySession();
            IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(streamId);

            object dado = eventos.Single().Data;
            dado.Should().BeOfType<PrazoDefinidoV2>();
            ((PrazoDefinidoV2)dado).Dias.Should().Be(30);
            ((PrazoDefinidoV2)dado).Origem.Should().Be("upcast-de-v1");
        }
    }
}
