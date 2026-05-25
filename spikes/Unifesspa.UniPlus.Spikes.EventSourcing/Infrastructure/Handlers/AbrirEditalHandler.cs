using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;

/// <summary>
/// Abre um edital iniciando o stream. Não há agregado a carregar (FetchForWriting),
/// então usa <c>IDocumentSession.Events.StartStream</c> diretamente. O
/// <c>SaveChangesAsync</c> é aplicado pela transactional middleware do Wolverine
/// (<c>AutoApplyTransactions</c>) — append + chave do titular commitam juntos.
/// </summary>
public static class AbrirEditalHandler
{
    public static async Task Handle(
        AbrirEdital comando,
        IDocumentSession session,
        IProtetorPii protetor,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(protetor);
        ArgumentNullException.ThrowIfNull(relogio);

        AtorCifrado ator = await protetor.ProtegerAsync(comando.Ator, cancellationToken).ConfigureAwait(false);

        EditalAberto evento = new(
            comando.EditalId,
            comando.NumeroEdital,
            comando.Titulo,
            ator,
            relogio.GetUtcNow());

        session.Events.StartStream<EditalEs>(comando.EditalId, evento);
    }
}
