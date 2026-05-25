using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.EventosIntegracao;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;
using Wolverine;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;

/// <summary>
/// Injeção de falha (prova de rollback, gate G1): anexa um evento ao stream e
/// publica uma mensagem de integração pelo outbox e então lança, antes que a
/// transactional middleware faça <c>SaveChangesAsync</c>. Como append e publish
/// compartilham a transação do handler, nenhum dos dois é commitado.
/// </summary>
public static class FalharAposAnexarHandler
{
    public static async Task Handle(
        FalharAposAnexar comando,
        IDocumentSession session,
        IMessageContext mensagens,
        IProtetorPii protetor,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mensagens);
        ArgumentNullException.ThrowIfNull(protetor);
        ArgumentNullException.ThrowIfNull(relogio);

        AtorCifrado ator = await protetor.ProtegerAsync(comando.Ator, cancellationToken).ConfigureAwait(false);
        DateTimeOffset agora = relogio.GetUtcNow();

        session.Events.Append(comando.EditalId, new EditalRetificado(comando.EditalId, comando.Motivo, ator, agora));
        await mensagens.PublishAsync(new EditalPublicadoIntegrado(comando.EditalId, "ROLLBACK", agora))
            .ConfigureAwait(false);

        throw new InvalidOperationException("Falha simulada após anexar evento e publicar mensagem de integração.");
    }
}
