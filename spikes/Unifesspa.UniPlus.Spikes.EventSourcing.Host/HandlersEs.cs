using Marten;
using Marten.Events;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Host;

/// <summary>
/// Handlers do event store <b>ancillary</b> (Marten). Injetam o store marcador
/// <see cref="IEditalEsStore"/> e abrem a sessão explicitamente.
/// <para>
/// <b>Achado:</b> num host em que o Marten é apenas ancillary (sem <c>AddMarten</c>
/// primário), a injeção de <c>IDocumentSession</c> via <c>[MartenStore]</c> não
/// resolve (o tipo ambiente não está registrado). O padrão robusto é injetar o
/// store marcador e gerir a sessão — o conflito de versão do <c>FetchForWriting</c>
/// é retentado pela política <c>OnException&lt;EventStreamUnexpectedMaxEventIdException&gt;</c>.
/// </para>
/// </summary>
public static class AbrirEditalEsHandler
{
    public static async Task Handle(AbrirEditalEs comando, IEditalEsStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(store);

        await using IDocumentSession session = store.LightweightSession();
        session.Events.StartStream<EditalEs>(comando.Id,
            new EditalAberto(comando.Id, comando.Numero, comando.Titulo, AtorEs.Ficticio(), DateTimeOffset.UtcNow));
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Retifica via concorrência otimista por stream (<c>FetchForWriting</c>).</summary>
public static class RetificarEditalEsHandler
{
    public static async Task Handle(RetificarEditalEs comando, IEditalEsStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(store);

        await using IDocumentSession session = store.LightweightSession();
        IEventStream<EditalEs> stream = await session.Events
            .FetchForWriting<EditalEs>(comando.Id, cancellationToken)
            .ConfigureAwait(false);

        stream.AppendOne(new EditalRetificado(comando.Id, comando.Motivo, AtorEs.Ficticio(), DateTimeOffset.UtcNow));
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class AtorEs
{
    public static AtorCifrado Ficticio() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), "host");
}
